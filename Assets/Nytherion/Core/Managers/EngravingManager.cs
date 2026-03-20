using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.GamePlay.Engravings;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using VContainer;
using VContainer.Unity;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.Core.Managers
{
    public class EngravingManager : BaseManager
    {
        [Header("Database")]
        [SerializeField] private EngravingDatabaseSO engravingDatabaseSO;

        [Header("Grid Settings")]
        [SerializeField] private int gridRows = 5;
        [SerializeField] private int gridColumns = 5;
        public int GridRows => gridRows;
        public int GridColumns => gridColumns;

        public event Action OnEngravingStateChanged;

        private EngravingGrid logicGrid;
        private Dictionary<string, EngravingData> engravingDatabase;
        private List<EngravingBlock> storageBlocks;
        private Dictionary<string, Vector2Int> placedBlockPositions;

        private EngravingBlock currentlyDraggedBlock;
        private bool isDraggingFromGrid;
        private Vector2Int dragStartPosition;

        public event Action<EngravingData, bool> OnEngravingEquippedStateChanged;

        protected override void Awake()
        {
            base.Awake();
            logicGrid = new EngravingGrid(gridRows, gridColumns);
            engravingDatabase = new Dictionary<string, EngravingData>();
            storageBlocks = new List<EngravingBlock>();
            placedBlockPositions = new Dictionary<string, Vector2Int>();
        }

        protected override void OnInitializeInternal()
        {
            LoadEngravingDatabaseFromSO();
        }

        #region 드래그 앤 드롭 관리

        public void StartDraggingFromGrid(EngravingBlock block, Vector2Int gridPosition)
        {
            if (block == null) return;
            currentlyDraggedBlock = block;
            isDraggingFromGrid = true;
            dragStartPosition = gridPosition;

            logicGrid.ClearBlockAt(gridPosition.y, gridPosition.x);
            placedBlockPositions.Remove(block.BlockId);
            logicGrid.RecalculateAllInfluences();

            if (block.SourceData != null)
            {
                OnEngravingEquippedStateChanged?.Invoke(block.SourceData, false);
            }
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

            if (dropGridPosition.HasValue && logicGrid.CanPlaceBlock(dropGridPosition.Value.y, dropGridPosition.Value.x))
            {
                PlaceBlockOnGrid(currentlyDraggedBlock, dropGridPosition.Value);
            }
            else if (!dropGridPosition.HasValue)
            {
                MoveToStorage(currentlyDraggedBlock);
            }
            else
            {
                ReturnDraggedBlockToOrigin();
            }

            logicGrid.RecalculateAllInfluences();
            currentlyDraggedBlock = null;
            OnEngravingStateChanged?.Invoke();
        }
        public void RotateDraggedBlock()
        {
            if (currentlyDraggedBlock != null)
            {
                currentlyDraggedBlock.Rotate();
                OnEngravingStateChanged?.Invoke();
            }
        }
        #endregion

        #region 내부 로직 메서드

        private void PlaceBlockOnGrid(EngravingBlock block, Vector2Int position)
        {
            logicGrid.PlaceBlock(block, position.y, position.x);
            placedBlockPositions.Add(block.BlockId, position);

            if (block.SourceData != null)
            {
                OnEngravingEquippedStateChanged?.Invoke(block.SourceData, true);
            }
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

            if (engravingDatabaseSO == null)
            {
                Debug.LogError("[EngravingManager] EngravingDatabaseSO가 null입니다!");
                return;
            }


            foreach (var engraving in engravingDatabaseSO.allEngravings)
            {
                if (engraving != null && !engravingDatabase.ContainsKey(engraving.engravingName))
                {
                    engravingDatabase.Add(engraving.engravingName, engraving);
                }
            }

            AddTestBlocks();
        }

        private void AddTestBlocks()
        {
            if (engravingDatabase.Count == 0)
            {
                return;
            }

            // 첫 번째 각인 데이터로 테스트 블록 생성
            var firstEngraving = engravingDatabase.Values.FirstOrDefault();
            if (firstEngraving != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    var testBlock = new EngravingBlock(firstEngraving, i + 1);
                    storageBlocks.Add(testBlock);
                }
            }
        }

        public IEnumerable<EngravingBlock> GetStorageBlocks()
        {
            return storageBlocks;
        }

        public IEnumerable<KeyValuePair<string, Vector2Int>> GetPlacedBlocks()
        {
            return placedBlockPositions;
        }

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
        public EngravingBlock GetBlockAt(int row, int col)
        {
            if (logicGrid == null) return null;
            return logicGrid.GetBlockAt(row, col);
        }
        public void AddNewEngravingToStorage(EngravingData data)
        {
            if (data == null) return;
            if (storageBlocks.Any(b => b.BlockId == data.engravingName) || placedBlockPositions.ContainsKey(data.engravingName)) return;

            var instanceData = Instantiate(data);
            var newBlock = new EngravingBlock(instanceData);
            storageBlocks.Add(newBlock);

            OnEngravingStateChanged?.Invoke();
        }

        private EngravingGridState GetEngravingsForSave()
        {
            var state = new EngravingGridState();
            if (placedBlockPositions == null || logicGrid == null)
            {
                return state;
            }

            foreach (var pair in placedBlockPositions)
            {
                EngravingBlock blockOnGrid = logicGrid.GetBlockAt(pair.Value.y, pair.Value.x);
                if (blockOnGrid != null)
                {
                    state.placedBlocks.Add(new EngravingGridState.SavedEngravingBlock
                    {
                        engravingId = blockOnGrid.BlockId,
                        gridRow = pair.Value.y,
                        gridCol = pair.Value.x,
                        rotationState = blockOnGrid.RotationState
                    });
                }
            }
            foreach (var block in storageBlocks)
            {
                state.placedBlocks.Add(new EngravingGridState.SavedEngravingBlock
                {
                    engravingId = block.BlockId,
                    gridRow = -1,
                    gridCol = -1,
                    rotationState = block.RotationState
                });
            }
            return state;
        }

        private void LoadDataFromSave(EngravingGridState state)
        {
            storageBlocks.Clear();
            logicGrid.Clear();
            placedBlockPositions.Clear();

            if (state == null || state.placedBlocks.Count == 0)
            {
                OnEngravingStateChanged?.Invoke();
                return;
            }

            foreach (var savedBlock in state.placedBlocks)
            {
                if (engravingDatabase.TryGetValue(savedBlock.engravingId, out EngravingData originalData))
                {
                    var instanceData = Instantiate(originalData);
                    var newBlock = new EngravingBlock(instanceData);
                    newBlock.SetRotationState(savedBlock.rotationState);

                    if (savedBlock.gridRow != -1)
                    {
                        var pos = new Vector2Int(savedBlock.gridCol, savedBlock.gridRow);
                        if (logicGrid.CanPlaceBlock(pos.y, pos.x))
                        {
                            PlaceBlockOnGrid(newBlock, pos);
                        }
                    }
                    else
                    {
                        storageBlocks.Add(newBlock);
                    }
                }
            }

            logicGrid.RecalculateAllInfluences();
            OnEngravingStateChanged?.Invoke();
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            saveData.engravingData = GetEngravingsForSave();
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            LoadDataFromSave(saveData.engravingData);
        }
        #endregion
    }
}