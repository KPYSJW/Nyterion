using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.GamePlay.Engravings;
using Nytherion.Data.ScriptableObjects.Engravings;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingGridUI : MonoBehaviour
    {
        public static EngravingGridUI Instance { get; private set; }

        [Header("Grid Settings")]
        public int rows = 20;
        public int columns = 20;
        public GameObject slotCellPrefab;
        public RectTransform gridRoot;
        public RectTransform placedBlocksContainer;
        [Header("Draggable Block Settings")]
        [SerializeField] private GameObject draggableBlockPrefab;
        [SerializeField] private Canvas rootCanvas;
        [Header("Block Storage Settings")]
        public RectTransform blockStorageParent;
        public GameObject storageSlotPrefab;
        private EngravingSlotCell[,] slotCells;
        private EngravingGrid logicGrid;

        private EngravingSlotCell currentPointerOverCell;
        public Vector2Int? CurrentGridPos => currentPointerOverCell?.GridPosition;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            InitializeGrid();
            TestPlaceSampleBlock();
        }

        public void AddNewBlockToStorage(EngravingBlock blockData)
        {
            if (blockData == null) return;

            GameObject slotObj = Instantiate(storageSlotPrefab, blockStorageParent);
            GameObject blockObj = Instantiate(draggableBlockPrefab, slotObj.transform);
            EngravingBlockDraggable draggable = blockObj.GetComponent<EngravingBlockDraggable>();

            draggable.blockData = blockData;
            draggable.BuildVisualFromShape();

            GridLayoutGroup mainGridLayout = gridRoot.GetComponent<GridLayoutGroup>();
            Vector2 cellSize = mainGridLayout.cellSize;
            Vector2 spacing = mainGridLayout.spacing;
            Vector2 visualCenterOffset = blockData.GetVisualCenterPixelOffset(cellSize, spacing);

            blockObj.GetComponent<RectTransform>().anchoredPosition = -visualCenterOffset;

            Debug.Log($"{blockData.BlockId} 블록이 보관소에 추가되었습니다.");
        }
        public void TestAddRandomBlock()
        {
            List<Vector2Int> shape = GenerateValidShape();

            EngravingData tempData = ScriptableObject.CreateInstance<EngravingData>();
            tempData.engravingName = "UserBlock_" + Random.Range(1000, 9999);
            tempData.shape = shape;
            tempData.isCursed = false;

            EngravingBlock newBlock = new EngravingBlock(tempData);
            AddNewBlockToStorage(newBlock);
        }
        private void TestPlaceSampleBlock()
        {
            List<Vector2Int> shape = new List<Vector2Int>
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1)
            };

            // 샘플용 EngravingData 생성
            EngravingData tempData = ScriptableObject.CreateInstance<EngravingData>();
            tempData.engravingName = "TestBlock";
            tempData.shape = shape;
            tempData.isCursed = false;

            EngravingBlock block = new EngravingBlock(tempData);
            logicGrid.PlaceBlock(block, 2, 2);
        }
#if UNITY_EDITOR
[ContextMenu("▶ Test Add Random Block")]
#endif
        public void EditorTestAddRandomBlock()
        {
            TestAddRandomBlock();
        }

        private List<Vector2Int> GenerateValidShape()
        {
            int targetCount = Random.Range(3, 6); // 3~5칸짜리
            List<Vector2Int> shape = new List<Vector2Int> { Vector2Int.zero };

            while (shape.Count < targetCount)
            {
                Vector2Int baseCell = shape[Random.Range(0, shape.Count)];
                Vector2Int[] directions = new Vector2Int[]
                {
                    Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
                };

                foreach (var dir in directions)
                {
                    Vector2Int newCell = baseCell + dir;
                    if (!shape.Contains(newCell))
                    {
                        shape.Add(newCell);
                        break;
                    }
                }
            }

            return shape;
        }



        private void InitializeGrid()
        {
            logicGrid = new EngravingGrid(rows, columns);
            slotCells = new EngravingSlotCell[rows, columns];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    GameObject cellGO = Instantiate(slotCellPrefab, gridRoot);
                    EngravingSlotCell cell = cellGO.GetComponent<EngravingSlotCell>();
                    cell.Initialize(new Vector2Int(x, y));
                    slotCells[y, x] = cell;
                }
            }

        }

        public void OnCellPointerEnter(EngravingSlotCell cell)
        {
            currentPointerOverCell = cell;
        }

        public void OnCellPointerExit(EngravingSlotCell cell)
        {
            if (currentPointerOverCell == cell)
            {
                currentPointerOverCell = null;
            }
        }
        public bool TryPlaceBlockAt(EngravingBlock block, Vector2Int centerCell)
        {
            return logicGrid.PlaceBlock(block, centerCell.y, centerCell.x);
        }

        public void ShowPlacementPreview(EngravingBlock block, Vector2Int gridPos)
        {
            ClearPreview();

            foreach (Vector2Int offset in block.Shape)
            {
                int x = gridPos.x + offset.x;
                int y = gridPos.y + offset.y;

                if (x >= 0 && x < columns && y >= 0 && y < rows)
                {
                    slotCells[y, x].Highlight(true);
                }
            }
        }
        public void ClearPreview()
        {
            foreach (var cell in slotCells)
            {
                cell.Highlight(false);
            }
        }
        public bool CanPlacePreview(EngravingBlock block, Vector2Int centerCell)
        {
            return logicGrid.CanPlaceBlock(block, centerCell.y, centerCell.x);
        }
        public Vector2 GetLocalPositionFromGridCell(Vector2Int gridPos)
        {
            if (gridPos.y < 0 || gridPos.y >= rows || gridPos.x < 0 || gridPos.x >= columns)
            {
                Debug.LogError("GetLocalPositionFromGridCell: 유효하지 않은 gridPos가 입력되었습니다.");
                return Vector2.zero;
            }

            RectTransform targetCellRect = slotCells[gridPos.y, gridPos.x].GetComponent<RectTransform>();

            Vector3 targetWorldPosition = targetCellRect.position;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, targetWorldPosition);
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                placedBlocksContainer,
                screenPoint,
                rootCanvas.worldCamera,
                out localPoint);

            return localPoint;
        }
        public bool RemoveBlockFromGrid(string blockId)
        {
            return logicGrid.RemoveBlock(blockId);
        }
        public void AddEngravingToStorage(EngravingData engravingData)
        {
            EngravingBlock block = new EngravingBlock(engravingData);

            GameObject slotObj = Instantiate(storageSlotPrefab, blockStorageParent);
            GameObject blockObj = Instantiate(draggableBlockPrefab, slotObj.transform);

            EngravingBlockDraggable draggable = blockObj.GetComponent<EngravingBlockDraggable>();
            draggable.blockData = block;
            draggable.BuildVisualFromShape();

            GridLayoutGroup gridLayout = gridRoot.GetComponent<GridLayoutGroup>();
            Vector2 cellSize = gridLayout.cellSize;
            Vector2 spacing = gridLayout.spacing;
            Vector2 visualOffset = block.GetVisualCenterPixelOffset(cellSize, spacing);
            blockObj.GetComponent<RectTransform>().anchoredPosition = -visualOffset;
        }

    }
}