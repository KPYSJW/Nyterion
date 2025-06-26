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

        [Header("UI 구성요소")]
        [SerializeField] private GameObject slotCellPrefab;
        public RectTransform gridRoot;
        public RectTransform placedBlocksContainer;

        [Header("드래그 블럭 설정")]
        [SerializeField] private GameObject draggableBlockPrefab;
        public Canvas rootCanvas;

        [Header("보관소 설정")]
        [SerializeField] public RectTransform blockStorageParent;
        [SerializeField] public GameObject storageSlotPrefab;

        [Header("영향 범위 기즈모")]
        [Tooltip("레벨 업 효과를 표시할 프리팹")]
        [SerializeField] private GameObject levelUpGizmoPrefab;
        [Tooltip("레벨 다운 효과를 표시할 프리팹")]
        [SerializeField] private GameObject levelDownGizmoPrefab;

        private EngravingSlotCell[,] slotCells;
        private EngravingSlotCell currentPointerOverCell;
        public Vector2Int? CurrentGridPos => currentPointerOverCell?.GridPosition;

        private int rows;
        private int columns;
        
        private GameObject[,] influenceGizmos;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public IEnumerator Initialize()
        {
            if (EngravingManager.Instance == null)
            {
                Debug.LogError("EngravingManager를 찾을 수 없어 UI를 초기화할 수 없습니다.");
                yield break;
            }

            this.rows = EngravingManager.Instance.GridRows;
            this.columns = EngravingManager.Instance.GridColumns;

            InitializeGridCells();
            yield return RefreshAllUICoroutine();
        }

        private void OnEnable()
        {
            if (EngravingManager.Instance != null)
            {
                EngravingManager.Instance.OnEngravingStateChanged += HandleEngravingStateChanged;
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

        private void HandleEngravingStateChanged()
        {
            if (gameObject.activeInHierarchy && EngravingManager.Instance != null)
            {
                StartCoroutine(RefreshAllUICoroutine());
            }
        }

        private IEnumerator RefreshAllUICoroutine()
        {
            yield return new WaitForEndOfFrame();

            ClearAllVisuals();

            foreach (var block in EngravingManager.Instance.GetStorageBlocks())
            {
                CreateBlockInStorage(block);
            }

            foreach (var pair in EngravingManager.Instance.GetPlacedBlocks())
            {
                var block = EngravingManager.Instance.GetBlockByID(pair.Key);
                if (block != null)
                {
                    CreateBlockOnGrid(block, pair.Value);
                }
            }

            DrawInfluenceGizmos();
        }
        
        private void InitializeGridCells()
        {
            foreach (Transform child in gridRoot) Destroy(child.gameObject);
            
            slotCells = new EngravingSlotCell[rows, columns];
            influenceGizmos = new GameObject[rows, columns]; 

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
        
        private void ClearAllVisuals()
        {
            foreach (Transform child in placedBlocksContainer) Destroy(child.gameObject);
            foreach (Transform child in blockStorageParent) Destroy(child.gameObject);

            if (influenceGizmos != null)
            {
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        if (influenceGizmos[y, x] != null)
                        {
                            Destroy(influenceGizmos[y, x]);
                        }
                    }
                }
            }
        }

        private void DrawInfluenceGizmos()
        {
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    InfluenceType influence = EngravingManager.Instance.GetInfluenceAt(y, x);
                    GameObject prefabToUse = null;

                    if (influence == InfluenceType.LevelUp)
                        prefabToUse = levelUpGizmoPrefab;
                    else if (influence == InfluenceType.LevelDown)
                        prefabToUse = levelDownGizmoPrefab;

                    if (prefabToUse != null)
                    {
                        Vector2 cellPosition = GetLocalPositionFromGridCell(new Vector2Int(x, y));
                        GameObject gizmo = Instantiate(prefabToUse, placedBlocksContainer);
                        gizmo.GetComponent<RectTransform>().anchoredPosition = cellPosition;
                        influenceGizmos[y, x] = gizmo;
                    }
                }
            }
        }

        private void CreateBlockInStorage(EngravingBlock blockData)
        {
            GameObject slotObj = Instantiate(storageSlotPrefab, blockStorageParent);
            GameObject blockObj = Instantiate(draggableBlockPrefab, slotObj.transform);
            var draggable = blockObj.GetComponent<EngravingBlockDraggable>();
            
            draggable.isPlaced = false;
            draggable.blockData = blockData;
            draggable.BuildVisualFromShape();
        }

        private void CreateBlockOnGrid(EngravingBlock blockData, Vector2Int position)
        {
            GameObject blockObj = Instantiate(draggableBlockPrefab, placedBlocksContainer);
            var draggable = blockObj.GetComponent<EngravingBlockDraggable>();

            draggable.isPlaced = true;
            draggable.gridPosition = position;
            draggable.blockData = blockData;
            draggable.BuildVisualFromShape();

            draggable.GetComponent<RectTransform>().anchoredPosition = GetLocalPositionFromGridCell(position);
        }

        public void OnCellPointerEnter(EngravingSlotCell cell) => currentPointerOverCell = cell;
        public void OnCellPointerExit(EngravingSlotCell cell) { if (currentPointerOverCell == cell) currentPointerOverCell = null; }

        public void ShowPlacementPreview(EngravingBlock block, Vector2Int gridPos)
        {
            ClearPreview();
            if (gridPos.x >= 0 && gridPos.x < columns && gridPos.y >= 0 && gridPos.y < rows)
            {
                slotCells[gridPos.y, gridPos.x].Highlight(true);
            }
        }

        public void ClearPreview()
        {
            foreach (var cell in slotCells) cell.Highlight(false);
        }

        public Vector2 GetLocalPositionFromGridCell(Vector2Int gridPos)
        {
            if (slotCells == null || gridPos.y < 0 || gridPos.y >= rows || gridPos.x < 0 || gridPos.x >= columns) return Vector2.zero;
            
            RectTransform targetCellRect = slotCells[gridPos.y, gridPos.x].GetComponent<RectTransform>();
            return placedBlocksContainer.InverseTransformPoint(targetCellRect.position);
        }
    }
}