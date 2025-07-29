using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Engravings;
using Zenject;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingGridUI : MonoBehaviour
    {
        private EngravingManager engravingManager;
        private DiContainer container;

        [Header("UI 구성요소")]
        [SerializeField] private GameObject slotCellPrefab;
        public RectTransform gridRoot;
        public RectTransform placedBlocksContainer;
        public RectTransform previewContainer;

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

        [Inject]
        public void Construct(EngravingManager engravingManager, DiContainer container)
        {
            this.engravingManager = engravingManager;
            this.container = container;
        }

        public IEnumerator Initialize()
        {
            if (engravingManager == null)
            {
                Debug.LogError("EngravingManager를 찾을 수 없어 UI를 초기화할 수 없습니다.");
                yield break;
            }

            this.rows = engravingManager.GridRows;
            this.columns = engravingManager.GridColumns;

            engravingManager.OnEngravingStateChanged -= HandleEngravingStateChanged; // 중복 구독 방지
            engravingManager.OnEngravingStateChanged += HandleEngravingStateChanged;

            InitializeGridCells();
            yield return RefreshAllUICoroutine();
        }

        private void OnEnable()
        {
            StartCoroutine(Initialize());
        }

        private void OnDisable()
        {
            if (engravingManager != null)
            {
                engravingManager.OnEngravingStateChanged -= HandleEngravingStateChanged;
            }
        }

        private void HandleEngravingStateChanged()
        {
            if (gameObject.activeInHierarchy && engravingManager != null)
            {
                StartCoroutine(RefreshAllUICoroutine());
            }
        }

        private IEnumerator RefreshAllUICoroutine()
        {
            yield return new WaitForEndOfFrame();

            ClearAllVisuals();

            if (engravingManager.GetStorageBlocks() != null)
            {
                foreach (EngravingBlock block in engravingManager.GetStorageBlocks())
                {
                    CreateBlockInStorage(block);
                }
            }

            if (engravingManager.GetPlacedBlocks() != null)
            {
                foreach (KeyValuePair<string, Vector2Int> pair in engravingManager.GetPlacedBlocks())
                {
                    EngravingBlock block = engravingManager.GetBlockByID(pair.Key);
                    if (block != null)
                    {
                        CreateBlockOnGrid(block, pair.Value);
                    }
                }
            }

            DrawInfluenceGizmos();
        }

        private void InitializeGridCells()
        {
            if (slotCells != null && slotCells.Length > 0) return;

            foreach (Transform child in gridRoot) Destroy(child.gameObject);

            this.rows = engravingManager.GridRows;
            this.columns = engravingManager.GridColumns;

            slotCells = new EngravingSlotCell[rows, columns];
            influenceGizmos = new GameObject[rows, columns];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    GameObject cellGO = Instantiate(slotCellPrefab, gridRoot);
                    EngravingSlotCell cell = cellGO.GetComponent<EngravingSlotCell>();
                    if (cell == null)
                    {
                        Debug.LogError($"EngravingSlotCell component not found on prefab for cell at ({x}, {y}).");
                        continue;
                    }
                    cell.Initialize(new Vector2Int(x, y));
                    cell.OnCellPointerEnter += OnCellPointerEnter;
                    cell.OnCellPointerExit += OnCellPointerExit;
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
                        if (influenceGizmos[y, x] != null) Destroy(influenceGizmos[y, x]);
                    }
                }
            }
            foreach (Transform child in previewContainer) Destroy(child.gameObject);
        }


        private void DrawInfluenceGizmos()
        {
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    InfluenceType influence = engravingManager.GetInfluenceAt(y, x);
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
            GameObject blockObj = container.InstantiatePrefab(draggableBlockPrefab, slotObj.transform);
            EngravingBlockDraggable draggable = blockObj.GetComponent<EngravingBlockDraggable>();

            draggable.isPlaced = false;
            draggable.blockData = blockData;
            draggable.BuildVisualFromShape();
        }

        private void CreateBlockOnGrid(EngravingBlock blockData, Vector2Int position)
        {
            GameObject blockObj = container.InstantiatePrefab(draggableBlockPrefab, placedBlocksContainer);
            EngravingBlockDraggable draggable = blockObj.GetComponent<EngravingBlockDraggable>();

            draggable.isPlaced = true;
            draggable.gridPosition = position;
            draggable.blockData = blockData;
            draggable.BuildVisualFromShape();

            draggable.GetComponent<RectTransform>().anchoredPosition = GetLocalPositionFromGridCell(position);
        }

        public void OnCellPointerEnter(EngravingSlotCell cell) => currentPointerOverCell = cell;
        public void OnCellPointerExit(EngravingSlotCell cell) { if (currentPointerOverCell == cell) currentPointerOverCell = null; }

        public void ShowPlacementPreview(EngravingBlock block, Vector2Int? gridPos)
        {
            foreach (Transform child in previewContainer) Destroy(child.gameObject);
            ClearPreview();

            if (block == null || !gridPos.HasValue) return;

            Vector2Int pos = gridPos.Value;
            if (pos.x >= 0 && pos.x < columns && pos.y >= 0 && pos.y < rows)
            {
                slotCells[pos.y, pos.x].Highlight(true);
            }

            foreach (var zone in block.GetRotatedInfluenceZones())
            {
                int targetRow = pos.y - zone.offset.y;
                int targetCol = pos.x + zone.offset.x;

                if (targetRow >= 0 && targetRow < rows && targetCol >= 0 && targetCol < columns)
                {
                    GameObject prefabToUse = null;
                    if (zone.type == InfluenceType.LevelUp) prefabToUse = levelUpGizmoPrefab;
                    else if (zone.type == InfluenceType.LevelDown) prefabToUse = levelDownGizmoPrefab;

                    if (prefabToUse != null)
                    {
                        Vector2 cellPosition = GetLocalPositionFromGridCell(new Vector2Int(targetCol, targetRow));
                        GameObject gizmo = Instantiate(prefabToUse, previewContainer);
                        gizmo.GetComponent<RectTransform>().anchoredPosition = cellPosition;

                        Graphic graphic = gizmo.GetComponent<Graphic>();
                        if (graphic != null)
                        {
                            Color color = graphic.color;
                            color.a = 0.5f;
                            graphic.color = color;
                        }
                    }
                }
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