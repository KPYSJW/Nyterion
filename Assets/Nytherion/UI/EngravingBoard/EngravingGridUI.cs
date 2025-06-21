using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Core;
using Nytherion.GamePlay.Engravings;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingGridUI : MonoBehaviour
    {
        public static EngravingGridUI Instance { get; private set; }

        [SerializeField] private GameObject slotCellPrefab;
        public RectTransform gridRoot;
        public RectTransform placedBlocksContainer;

        [Header("Draggable Block Settings")]
        [SerializeField] private GameObject draggableBlockPrefab;
        public Canvas rootCanvas;

        [Header("Block Storage Settings")]
        [SerializeField] public RectTransform blockStorageParent;
        [SerializeField] public GameObject storageSlotPrefab;

        private EngravingSlotCell[,] slotCells;
        private EngravingSlotCell currentPointerOverCell;
        public Vector2Int? CurrentGridPos => currentPointerOverCell?.GridPosition;

        private int rows;
        private int columns;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public IEnumerator Initialize()
        {
            if (EngravingManager.Instance == null)
            {
                Debug.LogError("EngravingManager instance not found! EngravingGridUI cannot initialize.");
                yield break;
            }

            this.rows = EngravingManager.Instance.GridRows;
            this.columns = EngravingManager.Instance.GridColumns;

            InitializeGridCells();
            
            // StartCoroutine을 제거하고, IEnumerator 자체를 yield return 합니다.
            // 이렇게 하면 GameInitializer가 이어서 이 코루틴을 실행하게 됩니다.
            yield return RefreshAllUICoroutine();
        }

        private void OnEnable()
        {
            if (EngravingManager.Instance != null)
            {
                EngravingManager.Instance.OnEngravingStateChanged += HandleEngravingStateChanged;
                // UI가 활성화될 때도 최신 상태를 반영하도록 호출
                HandleEngravingStateChanged();
            }
        }

        private void OnDisable()
        {
            if (EngravingManager.Instance != null)
            {
                EngravingManager.Instance.OnEngravingStateChanged -= HandleEngravingStateChanged;
            }
        }

        // 이벤트가 발생하면 이 핸들러가 코루틴을 시작합니다.
        // 이 시점에는 UI가 활성화된 상태이므로 StartCoroutine이 정상 동작합니다.
        private void HandleEngravingStateChanged()
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(RefreshAllUICoroutine());
            }
        }

        // 실제 UI 갱신 로직을 모두 포함하는 단일 코루틴
        private IEnumerator RefreshAllUICoroutine()
        {
            // UI 요소들의 레이아웃 계산을 위해 한 프레임 대기
            yield return new WaitForEndOfFrame();

            ClearAllBlockUI();

            if (EngravingManager.Instance == null) yield break;

            // 보관소 블록 다시 그리기
            if (EngravingManager.Instance.GetStorageBlocks() != null)
            {
                foreach (var block in EngravingManager.Instance.GetStorageBlocks())
                {
                    CreateBlockInStorage(block);
                }
            }

            // 그리드 블록 다시 그리기
            if (EngravingManager.Instance.GetPlacedBlocks() != null)
            {
                foreach (var pair in EngravingManager.Instance.GetPlacedBlocks())
                {
                    EngravingBlock block = EngravingManager.Instance.GetBlockByID(pair.Key);
                    if (block != null)
                    {
                        CreateBlockOnGrid(block, pair.Value);
                    }
                }
            }
        }

        private void InitializeGridCells()
        {
            foreach (Transform child in gridRoot)
            {
                Destroy(child.gameObject);
            }
            
            slotCells = new EngravingSlotCell[rows, columns];
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    GameObject cellGO = Instantiate(slotCellPrefab, gridRoot);
                    var cell = cellGO.GetComponent<EngravingSlotCell>();
                    cell.Initialize(new Vector2Int(x, y));
                    slotCells[y, x] = cell;
                }
            }
        }
        
        // (이 아래로 나머지 메서드들은 수정 없이 그대로 유지)
        private void ClearAllBlockUI()
        {
            foreach (Transform child in placedBlocksContainer)
            {
                Destroy(child.gameObject);
            }
            foreach (Transform child in blockStorageParent)
            {
                Destroy(child.gameObject);
            }
        }

        private void CreateBlockInStorage(EngravingBlock blockData)
        {
            if (blockData == null) return;

            GameObject slotObj = Instantiate(storageSlotPrefab, blockStorageParent);
            GameObject blockObj = Instantiate(draggableBlockPrefab, slotObj.transform);
            var draggable = blockObj.GetComponent<EngravingBlockDraggable>();

            draggable.isPlaced = false;
            draggable.blockData = blockData;
            draggable.BuildVisualFromShape();

            GridLayoutGroup mainGridLayout = gridRoot.GetComponent<GridLayoutGroup>();
            Vector2 cellSize = mainGridLayout.cellSize;
            Vector2 spacing = mainGridLayout.spacing;
            Vector2 visualCenterOffset = blockData.GetVisualCenterPixelOffset(cellSize, spacing);

            blockObj.GetComponent<RectTransform>().anchoredPosition = -visualCenterOffset;
        }

        private void CreateBlockOnGrid(EngravingBlock blockData, Vector2Int position)
        {
            GameObject blockObj = Instantiate(draggableBlockPrefab, placedBlocksContainer);
            var draggable = blockObj.GetComponent<EngravingBlockDraggable>();

            draggable.isPlaced = true;
            draggable.blockData = blockData;
            draggable.BuildVisualFromShape();

            Vector2 finalPosition = GetLocalPositionFromGridCell(position);
            draggable.GetComponent<RectTransform>().anchoredPosition = finalPosition;
        }

        public void OnCellPointerEnter(EngravingSlotCell cell) => currentPointerOverCell = cell;
        public void OnCellPointerExit(EngravingSlotCell cell) { if (currentPointerOverCell == cell) currentPointerOverCell = null; }

        public void ShowPlacementPreview(EngravingBlock block, Vector2Int gridPos)
        {
            ClearPreview();
            foreach (Vector2Int offset in block.Shape)
            {
                int x = gridPos.x + offset.x;
                int y = gridPos.y + offset.y;
                if (x >= 0 && x < columns && y >= 0 && y < rows)
                {
                    if (slotCells[y, x] != null) slotCells[y, x].Highlight(true);
                }
            }
        }

        public void ClearPreview()
        {
            foreach (var cell in slotCells)
            {
                if (cell != null) cell.Highlight(false);
            }
        }

        public Vector2 GetLocalPositionFromGridCell(Vector2Int gridPos)
        {
            if (slotCells == null || gridPos.y < 0 || gridPos.y >= rows || gridPos.x < 0 || gridPos.x >= columns)
            {
                return Vector2.zero;
            }

            RectTransform targetCellRect = slotCells[gridPos.y, gridPos.x].GetComponent<RectTransform>();
            Vector3 cellWorldPosition = targetCellRect.TransformPoint(targetCellRect.rect.center);
            Vector2 localPoint = placedBlocksContainer.InverseTransformPoint(cellWorldPosition);
            return localPoint;
        }
    }
}