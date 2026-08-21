using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Relics;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.RelicBoard
{
    public class RelicGridUI : MonoBehaviour
    {
        private RelicManager relicManager;
        private IObjectResolver container;

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
        [Tooltip("비활성화(Silence) 효과를 표시할 프리팹")]
        [SerializeField] private GameObject silenceGizmoPrefab;
        [Tooltip("시너지 연결(SynergyLink) 효과를 표시할 프리팹")]
        [SerializeField] private GameObject synergyLinkGizmoPrefab;


        private RelicSlotCell[,] slotCells;
        private RelicSlotCell currentPointerOverCell;
        public Vector2Int? CurrentGridPos => currentPointerOverCell?.GridPosition;

        private int rows;
        private int columns;

        private GameObject[,] influenceGizmos;

        [Inject]
        public void Construct(RelicManager relicManager, IObjectResolver container)
        {
            this.relicManager = relicManager;
            this.container = container;
        }

        public IEnumerator Initialize()
        {

            if (relicManager == null)
            {
                Debug.LogError("[RelicGridUI] RelicManager를 찾을 수 없어 UI를 초기화할 수 없습니다.");
                yield break;
            }

            this.rows = relicManager.GridRows;
            this.columns = relicManager.GridColumns;

            InitializeGridCells();
            yield return RefreshAllUICoroutine();

        }

        private void OnEnable()
        {
            if (relicManager != null)
            {
                relicManager.OnRelicStateChanged += HandleRelicStateChanged;
                HandleRelicStateChanged();
            }
        }

        private void OnDisable()
        {
            if (relicManager != null)
            {
                relicManager.OnRelicStateChanged -= HandleRelicStateChanged;
            }
        }

        private void HandleRelicStateChanged()
        {
            if (gameObject.activeInHierarchy && relicManager != null)
            {
                StartCoroutine(RefreshAllUICoroutine());
            }
        }

        private IEnumerator RefreshAllUICoroutine()
        {
            yield return new WaitForEndOfFrame();

            ClearAllVisuals();

            var storageBlocks = relicManager.GetStorageBlocks();

            foreach (RelicBlock block in storageBlocks)
            {
                CreateBlockInStorage(block);
            }

            var placedBlocks = relicManager.GetPlacedBlocks();

            foreach (KeyValuePair<string, Vector2Int> pair in placedBlocks)
            {
                RelicBlock block = relicManager.GetBlockByID(pair.Key);
                if (block != null)
                {
                    CreateBlockOnGrid(block, pair.Value);
                }
                else
                {
                    Debug.LogWarning($"[RelicGridUI] ID {pair.Key}에 해당하는 블록을 찾을 수 없습니다.");
                }
            }

            DrawInfluenceGizmos();
        }

        private void InitializeGridCells()
        {
            foreach (Transform child in gridRoot) Destroy(child.gameObject);

            slotCells = new RelicSlotCell[rows, columns];
            influenceGizmos = new GameObject[rows, columns];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    GameObject cellGO = Instantiate(slotCellPrefab, gridRoot);
                    RelicSlotCell cell = cellGO.GetComponent<RelicSlotCell>();
                    if (cell == null)
                    {
                        Debug.LogError($"RelicSlotCell component not found on prefab for cell at ({x}, {y}).");
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
                    InfluenceType influence = relicManager.GetInfluenceAt(y, x);
                    GameObject prefabToUse = null;
                    bool isSilence = false;

                    if (influence == InfluenceType.LevelUp)
                        prefabToUse = levelUpGizmoPrefab;
                    else if (influence == InfluenceType.LevelDown)
                        prefabToUse = levelDownGizmoPrefab;
                    else if (influence == InfluenceType.Silence)
                    {
                        prefabToUse = silenceGizmoPrefab != null ? silenceGizmoPrefab : levelDownGizmoPrefab;
                        isSilence = true;
                    }
                    else if (influence == InfluenceType.SynergyLink)
                    {
                        prefabToUse = synergyLinkGizmoPrefab != null ? synergyLinkGizmoPrefab : levelUpGizmoPrefab;
                    }

                    if (prefabToUse != null)
                    {
                        Vector2 cellPosition = GetLocalPositionFromGridCell(new Vector2Int(x, y));
                        GameObject gizmo = Instantiate(prefabToUse, placedBlocksContainer);
                        gizmo.GetComponent<RectTransform>().anchoredPosition = cellPosition;
                        influenceGizmos[y, x] = gizmo;

                        if (isSilence && silenceGizmoPrefab == null)
                        {
                            Graphic graphic = gizmo.GetComponent<Graphic>();
                            if (graphic != null) graphic.color = new Color(0.5f, 0, 0.5f, 1f); // 보라색
                        }
                        else if (influence == InfluenceType.SynergyLink && synergyLinkGizmoPrefab == null)
                        {
                            Graphic graphic = gizmo.GetComponent<Graphic>();
                            if (graphic != null) graphic.color = new Color(1f, 0.8f, 0f, 1f); // 노란색/금색
                        }
                    }
                }
            }
        }

        private void CreateBlockInStorage(RelicBlock blockData)
        {

            try
            {
                GameObject slotObj = Instantiate(storageSlotPrefab, blockStorageParent);

                GameObject blockObj = container.Instantiate(draggableBlockPrefab, slotObj.transform);

                RelicBlockDraggable draggable = blockObj.GetComponent<RelicBlockDraggable>();

                if (draggable != null)
                {
                    draggable.isPlaced = false;
                    draggable.blockData = blockData;
                    draggable.BuildVisualFromShape();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RelicGridUI] 블록 생성 중 오류 발생: {e.Message}\n{e.StackTrace}");
            }
        }

        private void CreateBlockOnGrid(RelicBlock blockData, Vector2Int position)
        {
            GameObject blockObj = container.Instantiate(draggableBlockPrefab, placedBlocksContainer);
            RelicBlockDraggable draggable = blockObj.GetComponent<RelicBlockDraggable>();

            draggable.isPlaced = true;
            draggable.gridPosition = position;
            draggable.blockData = blockData;
            draggable.BuildVisualFromShape();

            draggable.GetComponent<RectTransform>().anchoredPosition = GetLocalPositionFromGridCell(position);
        }

        public void OnCellPointerEnter(RelicSlotCell cell) => currentPointerOverCell = cell;
        public void OnCellPointerExit(RelicSlotCell cell) { if (currentPointerOverCell == cell) currentPointerOverCell = null; }

        public void ShowPlacementPreview(RelicBlock block, Vector2Int? gridPos)
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
                    bool isSilence = false;

                    if (zone.type == InfluenceType.LevelUp) prefabToUse = levelUpGizmoPrefab;
                    else if (zone.type == InfluenceType.LevelDown) prefabToUse = levelDownGizmoPrefab;
                    else if (zone.type == InfluenceType.Silence)
                    {
                        prefabToUse = silenceGizmoPrefab != null ? silenceGizmoPrefab : levelDownGizmoPrefab;
                        isSilence = true;
                    }
                    else if (zone.type == InfluenceType.SynergyLink)
                    {
                        prefabToUse = synergyLinkGizmoPrefab != null ? synergyLinkGizmoPrefab : levelUpGizmoPrefab;
                    }

                    if (prefabToUse != null)
                    {
                        Vector2 cellPosition = GetLocalPositionFromGridCell(new Vector2Int(targetCol, targetRow));
                        GameObject gizmo = Instantiate(prefabToUse, previewContainer);
                        gizmo.GetComponent<RectTransform>().anchoredPosition = cellPosition;

                        Graphic graphic = gizmo.GetComponent<Graphic>();
                        if (graphic != null)
                        {
                            Color color;
                            if (isSilence && silenceGizmoPrefab == null) color = new Color(0.5f, 0, 0.5f);
                            else if (zone.type == InfluenceType.SynergyLink && synergyLinkGizmoPrefab == null) color = new Color(1f, 0.8f, 0f);
                            else color = graphic.color;

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