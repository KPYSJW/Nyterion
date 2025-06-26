using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.GamePlay.Engravings;
using Nytherion.Services;
using Nytherion.Core.Interfaces;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace Nytherion.Core
{
    public class EngravingManager : MonoBehaviour
    {
        public static EngravingManager Instance { get; private set; }

        [Header("Database")]
        [SerializeField] private EngravingDatabaseSO engravingDatabaseSO;

        [Header("Grid Settings")]
        [SerializeField] private int gridRows = 5;
        [SerializeField] private int gridColumns = 4;
        public int GridRows => gridRows;
        public int GridColumns => gridColumns;

        public event Action OnInitialized;
        public event Action OnEngravingStateChanged;

        private EngravingGrid logicGrid;
        private IEngravingSaveService saveService;
        private Dictionary<string, EngravingData> engravingDatabase;
        private List<EngravingBlock> storageBlocks;
        private Dictionary<string, Vector2Int> placedBlockPositions;

        // 드래그 상태 관리를 위한 변수
        private EngravingBlock currentlyDraggedBlock;
        private bool isDraggingFromGrid;
        private Vector2Int dragStartPosition;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void Initialize()
        {
            logicGrid = new EngravingGrid(gridRows, gridColumns);
            engravingDatabase = new Dictionary<string, EngravingData>();
            storageBlocks = new List<EngravingBlock>();
            placedBlockPositions = new Dictionary<string, Vector2Int>();
            saveService = new PlayerPrefsEngravingSaveService();
            LoadEngravingDatabaseFromSO();
            LoadGrid();
            OnInitialized?.Invoke();
        }

        private void OnDestroy()
        {
            SaveGrid();
        }

        #region 드래그 앤 드롭 관리 (최종 수정 로직)

        public void StartDraggingFromGrid(EngravingBlock block, Vector2Int gridPosition)
        {
            if (block == null) return;
            currentlyDraggedBlock = block;
            isDraggingFromGrid = true;
            dragStartPosition = gridPosition;

            logicGrid.ClearBlockAt(gridPosition.y, gridPosition.x);
            placedBlockPositions.Remove(block.BlockId);
            logicGrid.RecalculateAllInfluences();
            OnEngravingStateChanged?.Invoke();
        }

        public void StartDraggingFromStorage(EngravingBlock block)
        {
            if (block == null) return;
            currentlyDraggedBlock = block;
            isDraggingFromGrid = false;
            storageBlocks.RemoveAll(b => b.BlockId == block.BlockId);
            OnEngravingStateChanged?.Invoke();
        }

        public void EndDrag(Vector2Int? dropGridPosition)
        {
            if (currentlyDraggedBlock == null) return;

            // Case 1: 유효한 그리드 칸에 놓았을 경우
            if (dropGridPosition.HasValue && logicGrid.CanPlaceBlock(dropGridPosition.Value.y, dropGridPosition.Value.x))
            {
                PlaceBlockOnGrid(currentlyDraggedBlock, dropGridPosition.Value);
            }
            // Case 2: 그리드 밖(보관소 등)에 놓았을 경우
            else if (!dropGridPosition.HasValue)
            {
                MoveToStorage(currentlyDraggedBlock);
            }
            // Case 3: 유효하지 않은 그리드 칸에 놓았을 경우
            else
            {
                ReturnDraggedBlockToOrigin();
            }

            logicGrid.RecalculateAllInfluences();
            currentlyDraggedBlock = null;
            OnEngravingStateChanged?.Invoke();
            SaveGrid();
        }

        #endregion

        #region 내부 로직 메서드

        private void PlaceBlockOnGrid(EngravingBlock block, Vector2Int position)
        {
            logicGrid.PlaceBlock(block, position.y, position.x);
            placedBlockPositions.Add(block.BlockId, position);
        }

        private void ReturnDraggedBlockToOrigin()
        {
            if (isDraggingFromGrid)
            {
                PlaceBlockOnGrid(currentlyDraggedBlock, dragStartPosition);
            }
            else
            {
                MoveToStorage(currentlyDraggedBlock);
            }
        }

        private void MoveToStorage(EngravingBlock block)
        {
            if (!storageBlocks.Any(b => b.BlockId == block.BlockId))
            {
                storageBlocks.Add(block);
            }
        }
        
        public InfluenceType GetInfluenceAt(int row, int col)
        {
            return logicGrid?.GetInfluenceAt(row, col) ?? InfluenceType.None;
        }

        #endregion

        #region 데이터 로드/저장 및 유틸리티
        
        private void LoadEngravingDatabaseFromSO()
        {
            if (engravingDatabaseSO == null) return;
            foreach (var engraving in engravingDatabaseSO.allEngravings)
            {
                if (engraving != null && !engravingDatabase.ContainsKey(engraving.engravingName))
                {
                    engravingDatabase.Add(engraving.engravingName, engraving);
                }
            }
        }

        public IEnumerable<EngravingBlock> GetStorageBlocks() => storageBlocks;
        public IEnumerable<KeyValuePair<string, Vector2Int>> GetPlacedBlocks() => placedBlockPositions;

        public EngravingBlock GetBlockByID(string id)
        {
            var blockInStorage = storageBlocks.FirstOrDefault(b => b.BlockId == id);
            if (blockInStorage != null) return blockInStorage;

            if (placedBlockPositions.TryGetValue(id, out var pos))
            {
                return logicGrid.GetBlockAt(pos.y, pos.x);
            }
            return null;
        }

        public void AddNewEngravingToStorage(EngravingData data)
        {
            if (data == null) return;
            if (storageBlocks.Any(b => b.BlockId == data.engravingName) || placedBlockPositions.ContainsKey(data.engravingName)) return;

            var instanceData = Instantiate(data);
            var newBlock = new EngravingBlock(instanceData);
            storageBlocks.Add(newBlock);
            OnEngravingStateChanged?.Invoke();
            SaveGrid();
        }

        public void SaveGrid()
        {
            var state = new EngravingGridState();
            foreach (var pair in placedBlockPositions)
            {
                state.placedBlocks.Add(new EngravingGridState.SavedEngravingBlock
                {
                    engravingId = pair.Key,
                    gridRow = pair.Value.y,
                    gridCol = pair.Value.x
                });
            }
            foreach (var block in storageBlocks)
            {
                state.placedBlocks.Add(new EngravingGridState.SavedEngravingBlock
                {
                    engravingId = block.BlockId,
                    gridRow = -1,
                    gridCol = -1
                });
            }
            saveService.SaveEngravings(state);
        }

        public void LoadGrid()
        {
            var state = saveService.LoadEngravings();
            logicGrid.Clear();
            storageBlocks.Clear();
            placedBlockPositions.Clear();

            if (state == null) return;

            foreach (var savedBlock in state.placedBlocks)
            {
                if (engravingDatabase.TryGetValue(savedBlock.engravingId, out var originalData))
                {
                    var instanceData = Instantiate(originalData);
                    var newBlock = new EngravingBlock(instanceData);

                    if (savedBlock.gridRow == -1)
                    {
                        if (!storageBlocks.Any(b => b.BlockId == newBlock.BlockId))
                            storageBlocks.Add(newBlock);
                    }
                    else
                    {
                        var pos = new Vector2Int(savedBlock.gridCol, savedBlock.gridRow);
                        if (logicGrid.CanPlaceBlock(pos.y, pos.x))
                        {
                            PlaceBlockOnGrid(newBlock, pos);
                        }
                        else
                        {
                            if (!storageBlocks.Any(b => b.BlockId == newBlock.BlockId))
                                storageBlocks.Add(newBlock);
                        }
                    }
                }
            }
            
            logicGrid.RecalculateAllInfluences();
            OnEngravingStateChanged?.Invoke();
        }

        #endregion
    }
}