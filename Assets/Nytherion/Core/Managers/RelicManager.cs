using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Relics;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using VContainer;
using VContainer.Unity;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Systems;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Managers
{
    public class RelicManager : BaseManager
    {
        [Header("Database")]
        [SerializeField] private RelicDatabaseSO relicDatabaseSO;

        [Header("Grid Settings")]
        [SerializeField] private int gridRows = 5;
        [SerializeField] private int gridColumns = 5;
        public int GridRows => gridRows;
        public int GridColumns => gridColumns;

        public event Action OnRelicStateChanged;

        private RelicGrid logicGrid;
        private Dictionary<string, RelicData> relicDatabase;
        private List<RelicBlock> storageBlocks;
        private Dictionary<string, Vector2Int> placedBlockPositions;
        private Dictionary<string, int> maxChainLengthPerSeries = new Dictionary<string, int>();

        private RelicBlock currentlyDraggedBlock;
        private bool isDraggingFromGrid;
        private Vector2Int dragStartPosition;

        public event Action<RelicData, bool> OnRelicEquippedStateChanged;

        protected override void Awake()
        {
            base.Awake();
            logicGrid = new RelicGrid(gridRows, gridColumns);
            relicDatabase = new Dictionary<string, RelicData>();
            storageBlocks = new List<RelicBlock>();
            placedBlockPositions = new Dictionary<string, Vector2Int>();
        }

        protected override void OnInitializeInternal()
        {
            LoadRelicDatabaseFromSO();
        }

        #region 드래그 앤 드롭 관리

        public void StartDraggingFromGrid(RelicBlock block, Vector2Int gridPosition)
        {
            if (block == null) return;
            currentlyDraggedBlock = block;
            isDraggingFromGrid = true;
            dragStartPosition = gridPosition;

            logicGrid.ClearBlockAt(gridPosition.y, gridPosition.x);
            placedBlockPositions.Remove(block.BlockId);

            // 그리드 영향권에서 벗어나는 순간 상태 초기화
            block.ResetLevel();

            RefreshRelicLogic();

            if (block.SourceData != null)
            {
                OnRelicEquippedStateChanged?.Invoke(block.SourceData, false);
            }
            OnRelicStateChanged?.Invoke();
        }

        public void StartDraggingFromStorage(RelicBlock block)
        {
            if (block == null) return;
            currentlyDraggedBlock = block;
            isDraggingFromGrid = false;
            storageBlocks.RemoveAll(b => b.BlockId == block.BlockId);

            // 혹시 남아있을 수 있는 상태 초기화
            block.ResetLevel();

            OnRelicStateChanged?.Invoke();
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

            RefreshRelicLogic();
            currentlyDraggedBlock = null;
            OnRelicStateChanged?.Invoke();
        }
        public void RotateDraggedBlock()
        {
            if (currentlyDraggedBlock != null)
            {
                currentlyDraggedBlock.Rotate();
                OnRelicStateChanged?.Invoke();
            }
        }
        #endregion

        #region 내부 로직 메서드

        private void PlaceBlockOnGrid(RelicBlock block, Vector2Int position)
        {
            logicGrid.PlaceBlock(block, position.y, position.x);
            placedBlockPositions.Add(block.BlockId, position);

            if (block.SourceData != null)
            {
                OnRelicEquippedStateChanged?.Invoke(block.SourceData, true);
            }

            // 중앙의 자갈 (CenterPebble) 정중앙 배치 업적 연동
            if (block.RelicId == "CenterPebble")
            {
                int centerRow = gridRows / 2;
                int centerCol = gridColumns / 2;
                if (position.y == centerRow && position.x == centerCol)
                {
                    ProgressionManager progressionManager = DataLifetimeScope.Instance != null ? DataLifetimeScope.Instance.GetDataManager<ProgressionManager>() : null;
                    if (progressionManager != null)
                    {
                        progressionManager.ProcessAction(ProgressionType.CenterPebblePlace, 1);
                    }
                }
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

        private void MoveToStorage(RelicBlock block)
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

        public int GetMaxChainLength(string seriesId)
        {
            if (string.IsNullOrEmpty(seriesId)) return 0;
            if (maxChainLengthPerSeries.TryGetValue(seriesId, out int length))
            {
                return length;
            }
            return 0;
        }

        public void UpdateSynergyChains(Dictionary<string, int> newChainData)
        {
            maxChainLengthPerSeries = newChainData ?? new Dictionary<string, int>();
        }

        #endregion

        #region 데이터 로드/저장 및 유틸리티

        private void LoadRelicDatabaseFromSO()
        {

            if (relicDatabaseSO == null)
            {
                Debug.LogError("[RelicManager] RelicDatabaseSO가 null입니다!");
                return;
            }


            foreach (var relic in relicDatabaseSO.allRelics)
            {
                if (relic != null && !relicDatabase.ContainsKey(relic.relicName))
                {
                    relicDatabase.Add(relic.relicName, relic);
                }
            }

            AddTestBlocks();
        }

        private void AddTestBlocks()
        {
            if (relicDatabase.Count == 0)
            {
                return;
            }

            // 첫 번째 각인 데이터로 테스트 블록 생성
            var firstRelic = relicDatabase.Values.FirstOrDefault();
            if (firstRelic != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    var testBlock = new RelicBlock(firstRelic, i + 1);
                    storageBlocks.Add(testBlock);
                }
            }
        }

        public IEnumerable<RelicBlock> GetStorageBlocks()
        {
            return storageBlocks;
        }

        public IEnumerable<KeyValuePair<string, Vector2Int>> GetPlacedBlocks()
        {
            return placedBlockPositions;
        }

        public RelicBlock GetBlockByID(string id)
        {
            var blockInStorage = storageBlocks.FirstOrDefault(b => b.BlockId == id);
            if (blockInStorage != null) return blockInStorage;

            if (placedBlockPositions.TryGetValue(id, out var pos))
            {
                return logicGrid.GetBlockAt(pos.y, pos.x);
            }
            return null;
        }
        public RelicBlock GetBlockAt(int row, int col)
        {
            if (logicGrid == null) return null;
            return logicGrid.GetBlockAt(row, col);
        }
        public void AddNewRelicToStorage(RelicData data)
        {
            if (data == null) return;

            // 중복 방지 (선택 사항): 이미 같은 종류의 유물이 보관함이나 그리드에 있는지 확인
            bool isAlreadyInStorage = storageBlocks.Any(b => b.RelicId == data.relicName);
            bool isAlreadyOnGrid = placedBlockPositions.Values.Any(pos => 
            {
                var block = logicGrid.GetBlockAt(pos.y, pos.x);
                return block != null && block.RelicId == data.relicName;
            });

            if (isAlreadyInStorage || isAlreadyOnGrid) return;

            var instanceData = Instantiate(data);
            var newBlock = new RelicBlock(instanceData);
            storageBlocks.Add(newBlock);

            OnRelicStateChanged?.Invoke();
        }

        public void AddAllRelicsToStorage()
        {
            if (relicDatabaseSO == null || relicDatabaseSO.allRelics == null) return;

            foreach (RelicData relic in relicDatabaseSO.allRelics)
            {
                if (relic != null)
                {
                    AddNewRelicToStorage(relic);
                }
            }
        }

        private RelicGridState GetRelicsForSave()
        {
            var state = new RelicGridState();
            if (placedBlockPositions == null || logicGrid == null)
            {
                return state;
            }

            foreach (var pair in placedBlockPositions)
            {
                RelicBlock blockOnGrid = logicGrid.GetBlockAt(pair.Value.y, pair.Value.x);
                if (blockOnGrid != null)
                {
                    state.placedBlocks.Add(new RelicGridState.SavedRelicBlock
                    {
                        relicId = blockOnGrid.RelicId,
                        gridRow = pair.Value.y,
                        gridCol = pair.Value.x,
                        rotationState = blockOnGrid.RotationState
                    });
                }
            }
            foreach (var block in storageBlocks)
            {
                state.placedBlocks.Add(new RelicGridState.SavedRelicBlock
                {
                    relicId = block.RelicId,
                    gridRow = -1,
                    gridCol = -1,
                    rotationState = block.RotationState
                });
            }
            return state;
        }

        private void LoadDataFromSave(RelicGridState state)
        {
            storageBlocks.Clear();
            logicGrid.Clear();
            placedBlockPositions.Clear();

            if (state == null || state.placedBlocks.Count == 0)
            {
                OnRelicStateChanged?.Invoke();
                return;
            }

            foreach (var savedBlock in state.placedBlocks)
            {
                if (relicDatabase.TryGetValue(savedBlock.relicId, out RelicData originalData))
                {
                    var instanceData = Instantiate(originalData);
                    var newBlock = new RelicBlock(instanceData);
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

            RefreshRelicLogic();
            OnRelicStateChanged?.Invoke();
        }

        private void RefreshRelicLogic()
        {
            if (logicGrid == null) return;
            logicGrid.RecalculateAllInfluences();
            UpdateSynergyChains(logicGrid.EvaluateSynergyChains());
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            saveData.relicData = GetRelicsForSave();
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            LoadDataFromSave(saveData.relicData);
        }

        public bool IsRelicActive(string relicId)
        {
            if (placedBlockPositions == null || logicGrid == null) return false;

            foreach (KeyValuePair<string, Vector2Int> pair in placedBlockPositions)
            {
                RelicBlock block = logicGrid.GetBlockAt(pair.Value.y, pair.Value.x);
                if (block != null && block.RelicId == relicId && !block.SourceData.isDisabled)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}