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

        public event Action OnInitialized;

        private EngravingGrid logicGrid;
        private IEngravingSaveService saveService;
        private Dictionary<string, EngravingData> engravingDatabase;
        private List<EngravingBlock> storageBlocks;
        private Dictionary<string, Vector2Int> placedBlockPositions;

        [Header("Grid Settings")]
        [SerializeField] private int gridRows = 10;
        [SerializeField] private int gridColumns = 10;
        public int GridRows => gridRows;
        public int GridColumns => gridColumns;
        public event Action OnEngravingStateChanged;

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

        private void LoadEngravingDatabaseFromSO()
        {
            if (engravingDatabaseSO == null)
            {
                return;
            }

            foreach (EngravingData engraving in engravingDatabaseSO.allEngravings)
            {
                if (engraving != null && !engravingDatabase.ContainsKey(engraving.engravingName))
                {
                    engravingDatabase.Add(engraving.engravingName, engraving);
                }
            }
        }

        public IEnumerable<EngravingBlock> GetStorageBlocks() => storageBlocks;
        public IEnumerable<KeyValuePair<string, Vector2Int>> GetPlacedBlocks() => placedBlockPositions;
        public EngravingData GetEngravingData(string id)
        {
            engravingDatabase.TryGetValue(id, out EngravingData data);
            return data;
        }
        public EngravingBlock GetBlockByID(string id)
        {
            EngravingBlock block = storageBlocks.FirstOrDefault(b => b.BlockId == id);
            if (block != null) return block;

            if (placedBlockPositions.ContainsKey(id))
            {
                Vector2Int pos = placedBlockPositions[id];
                return logicGrid.GetBlockAt(pos.y, pos.x);
            }
            return null;
        }

        public void PickUpFromStorage(EngravingBlock block)
        {
            var blockToRemove = storageBlocks.FirstOrDefault(b => b.BlockId == block.BlockId);
            if (blockToRemove != null)
            {
                storageBlocks.Remove(blockToRemove);
                SaveGrid();
            }
        }

        public void PickUpFromGrid(EngravingBlock block)
        {
            if (placedBlockPositions.TryGetValue(block.BlockId, out Vector2Int position))
            {
                foreach (var offset in block.Shape)
                {
                    logicGrid.PlaceBlock(null, position.y + offset.y, position.x + offset.x);
                }
                placedBlockPositions.Remove(block.BlockId);
                SaveGrid();
            }
        }
        public bool TryPlaceBlock(EngravingBlock block, Vector2Int position)
        {
            if (logicGrid.CanPlaceBlock(block, position.y, position.x))
            {
                logicGrid.PlaceBlock(block, position.y, position.x);
                placedBlockPositions[block.BlockId] = position;
                OnEngravingStateChanged?.Invoke();
                SaveGrid();
                return true;
            }
            return false;
        }

        public void ReturnToStorage(EngravingBlock block)
        {
            if (!storageBlocks.Any(b => b.BlockId == block.BlockId))
            {
                storageBlocks.Add(block);
            }
            OnEngravingStateChanged?.Invoke();
            SaveGrid();
        }

        public bool CanPlaceBlock(EngravingBlock block, int row, int col)
        {
            return logicGrid.CanPlaceBlock(block, row, col);
        }

        public void AddNewEngravingToStorage(EngravingData data)
        {
            if (data == null) return;
            if (storageBlocks.Any(b => b.BlockId == data.engravingName) || placedBlockPositions.ContainsKey(data.engravingName)) return;

            EngravingData instanceData = UnityEngine.Object.Instantiate(data);
            EngravingBlock newBlock = new EngravingBlock(instanceData);
            storageBlocks.Add(newBlock);
            OnEngravingStateChanged?.Invoke();
            SaveGrid();
        }

        public void SaveGrid()
        {
            EngravingGridState state = new EngravingGridState();
            foreach (KeyValuePair<string, Vector2Int> pair in placedBlockPositions)
            {
                EngravingBlock block = logicGrid.GetBlockAt(pair.Value.y, pair.Value.x);
                if (block != null)
                {
                    state.placedBlocks.Add(new EngravingGridState.SavedEngravingBlock { engravingId = block.BlockId, gridRow = pair.Value.y, gridCol = pair.Value.x, shape = block.Shape });
                }
            }
            foreach (EngravingBlock block in storageBlocks)
            {
                state.placedBlocks.Add(new EngravingGridState.SavedEngravingBlock { engravingId = block.BlockId, gridRow = -1, gridCol = -1, shape = block.Shape });
            }

            string json = JsonUtility.ToJson(state, true);

            saveService.SaveEngravings(state);
        }
        public void LoadGrid()
        {
            EngravingGridState state = saveService.LoadEngravings();
            logicGrid.Clear();
            storageBlocks.Clear();
            placedBlockPositions.Clear();

            HashSet<string> loadedEngravingIds = new HashSet<string>();

            if (state != null && state.placedBlocks.Count > 0)
            {
                foreach (var savedBlock in state.placedBlocks)
                {
                    if (engravingDatabase.TryGetValue(savedBlock.engravingId, out EngravingData originalData))
                    {
                        loadedEngravingIds.Add(savedBlock.engravingId);
                        EngravingData instanceData = Instantiate(originalData);
                        instanceData.shape = new List<Vector2Int>(savedBlock.shape);
                        EngravingBlock newBlock = new EngravingBlock(instanceData);

                        if (savedBlock.gridRow == -1)
                        {
                            storageBlocks.Add(newBlock);
                        }
                        else
                        {
                            if (logicGrid.CanPlaceBlock(newBlock, savedBlock.gridRow, savedBlock.gridCol))
                            {
                                logicGrid.PlaceBlock(newBlock, savedBlock.gridRow, savedBlock.gridCol);
                                placedBlockPositions.Add(newBlock.BlockId, new Vector2Int(savedBlock.gridCol, savedBlock.gridRow));
                            }
                            else
                            {
                                storageBlocks.Add(newBlock);
                            }
                        }
                    }
                }
            }

            foreach (var dbEngraving in engravingDatabase.Values)
            {
                if (!loadedEngravingIds.Contains(dbEngraving.engravingName))
                {
                    EngravingData instanceData = Instantiate(dbEngraving);
                    EngravingBlock newBlock = new EngravingBlock(instanceData);
                    storageBlocks.Add(newBlock);
                }
            }

            OnEngravingStateChanged?.Invoke();
            SaveGrid();
        }
    }
}
