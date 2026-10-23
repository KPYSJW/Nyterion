using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Relics;
using VContainer;
using VContainer.Unity;
using Nytherion.UI.Components;

namespace Nytherion.UI.RelicBoard
{
    public class RelicGridUI : MonoBehaviour
    {
        private const float PreviewInfluenceAmountFontSize = 30f;
        public const int StorageColumns = 4;
        public const int StorageSlotsPerPage = StorageColumns * StorageColumns;
        public const float StorageSlotSize = 128f;
        public const float EquippedSlotSize = 96f;

        private RelicManager relicManager;
        private IObjectResolver container;
        private CurrencyDataManager currency;
        private AudioManager uiAudio;
        private RelicTooltip tooltip;

        [Header("유물 분해")]
        [SerializeField] private Button storageDismantleButton;
        [SerializeField] private RelicDismantlePanel dismantlePanel;
        public RelicDismantlePanel DismantlePanel => dismantlePanel;

        [Inject]
        public void BindDismantle(CurrencyDataManager currency, AudioManager audio, RelicTooltip tooltip)
        {
            this.currency = currency;
            uiAudio = audio;
            this.tooltip = tooltip;
        }

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
        [Header("보관소 페이지 UI")]
        [SerializeField] private RectTransform paginationRoot;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private Button storageSortButton;
        [SerializeField] private TextMeshProUGUI pageText;

        [Header("장착 유물 수")]
        [Tooltip("현재 장착 유물 수를 표시할 텍스트입니다. 비어 있으면 같은 캔버스의 RelicCountText를 자동으로 찾습니다.")]
        [SerializeField] private TextMeshProUGUI relicCountText;
        private bool relicCountHiddenForDismantle;
        private bool relicCountWasActive;

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
        private readonly List<GameObject> storageSlots = new List<GameObject>();
        private Coroutine refreshRoutine;
        private int storagePage;
        private int storagePageCount = 1;
        private sealed class PreviewGizmo
        {
            public GameObject prefab;
            public GameObject instance;
            public RectTransform rectTransform;
            public Graphic graphic;
            public Color originalColor;
            public TextMeshProUGUI amountLabel;
        }

        private readonly Dictionary<GameObject, Stack<PreviewGizmo>> previewPools =
            new Dictionary<GameObject, Stack<PreviewGizmo>>();
        private readonly List<PreviewGizmo> activePreviewGizmos = new List<PreviewGizmo>();
        public Vector2Int? CurrentGridPos => currentPointerOverCell?.GridPosition;

        private int rows;
        private int columns;

        [Inject]
        public void Construct(RelicManager relicManager, IObjectResolver container)
        {
            this.relicManager = relicManager;
            this.container = container;
            if (isActiveAndEnabled)
            {
                relicManager.OnRelicStateChanged -= HandleRelicStateChanged;
                relicManager.OnRelicStateChanged += HandleRelicStateChanged;
            }
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

            if (slotCells == null) InitializeGridCells();
            InitializeStorageSlots();
            if (dismantlePanel != null && currency != null)
            {
                dismantlePanel.Initialize(relicManager, currency, this, uiAudio, tooltip);
                storageDismantleButton.onClick.RemoveListener(dismantlePanel.Toggle);
                storageDismantleButton.onClick.AddListener(dismantlePanel.Toggle);
            }
            HandleRelicStateChanged();
            yield return null;

        }

        private void OnEnable()
        {
            if (relicManager != null)
            {
                relicManager.OnRelicStateChanged -= HandleRelicStateChanged;
                relicManager.OnRelicStateChanged += HandleRelicStateChanged;
                HandleRelicStateChanged();
            }
        }

        private void OnDisable()
        {
            CancelActiveDrag();
            CloseDismantle();
            if (refreshRoutine != null) StopCoroutine(refreshRoutine);
            refreshRoutine = null;
            ClearPreview();
            if (relicManager != null)
            {
                relicManager.OnRelicStateChanged -= HandleRelicStateChanged;
            }
        }

        private void HandleRelicStateChanged()
        {
            if (gameObject.activeInHierarchy && relicManager != null && slotCells != null && refreshRoutine == null)
            {
                refreshRoutine = StartCoroutine(RefreshAllUICoroutine());
            }
        }

        private IEnumerator RefreshAllUICoroutine()
        {
            // 같은 프레임의 장착·회전·레벨 이벤트를 하나의 갱신으로 합친다.
            yield return null;

            ClearAllVisuals();

            List<RelicBlock> storageBlocks = relicManager.GetStorageBlocks()
                .Where(block => block != null && block.SourceData != null && block.SourceData.IsRuntimeAvailable &&
                    !relicManager.IsStagedForDismantle(block))
                .ToList();
            storagePageCount = Mathf.Max(1, Mathf.CeilToInt(storageBlocks.Count / (float)StorageSlotsPerPage));
            storagePage = Mathf.Clamp(storagePage, 0, storagePageCount - 1);

            int firstIndex = storagePage * StorageSlotsPerPage;
            for (int i = 0; i < StorageSlotsPerPage && firstIndex + i < storageBlocks.Count; i++)
            {
                RelicBlock block = storageBlocks[firstIndex + i];
                if (!relicManager.IsBeingDragged(block)) CreateBlockInStorage(block, storageSlots[i]);
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

            UpdateRelicCountText();
            UpdatePagination();
            if (storageSortButton != null) storageSortButton.interactable = storageBlocks.Count > 1;
            refreshRoutine = null;

        }

        private void UpdateRelicCountText()
        {
            ResolveRelicCountText();
            if (relicCountText == null || relicManager == null) return;

            relicCountText.text = $"{relicManager.EquippedRelicCount}/{RelicManager.MaxEquippedRelics}";
        }

        private void ResolveRelicCountText()
        {
            if (relicCountText != null) return;

            Canvas searchCanvas = rootCanvas != null ? rootCanvas.rootCanvas : GetComponentInParent<Canvas>(true);
            if (searchCanvas == null) return;

            foreach (TextMeshProUGUI text in searchCanvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.gameObject.name == "RelicCountText")
                {
                    relicCountText = text;
                    return;
                }
            }
        }

        public void SetRelicCountHiddenForDismantle(bool hidden)
        {
            if (relicCountHiddenForDismantle == hidden) return;
            ResolveRelicCountText();
            if (relicCountText == null) return;

            if (hidden) relicCountWasActive = relicCountText.gameObject.activeSelf;
            relicCountHiddenForDismantle = hidden;
            relicCountText.gameObject.SetActive(hidden ? false : relicCountWasActive);
        }

        private void InitializeGridCells()
        {
            foreach (Transform child in gridRoot) RetireVisual(child.gameObject);
            GridLayoutGroup layout = gridRoot.GetComponent<GridLayoutGroup>();
            if (layout != null) layout.cellSize = Vector2.one * EquippedSlotSize;

            slotCells = new RelicSlotCell[rows, columns];

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
                    Image equippedSlotFrame = cell.BackgroundImage;
                    PixelPerfectSlotFrame.Apply(equippedSlotFrame);
                    cell.OnCellPointerEnter += OnCellPointerEnter;
                    cell.OnCellPointerExit += OnCellPointerExit;
                    slotCells[y, x] = cell;
                }
            }
        }

        private void ClearAllVisuals()
        {
            foreach (Transform child in placedBlocksContainer) RetireVisual(child.gameObject);
            foreach (RelicSlotCell cell in slotCells) cell?.ClearCell();
            foreach (GameObject slot in storageSlots)
            {
                slot.GetComponent<RelicSlotVisual>()?.SetRarity(null);
                foreach (RelicBlockDraggable draggable in slot.GetComponentsInChildren<RelicBlockDraggable>())
                    RetireVisual(draggable.gameObject);
                Image icon = slot.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.enabled = false;
                    icon.sprite = null;
                }
            }
            ClearPreview();
        }

        private static void RetireVisual(GameObject visual)
        {
            visual.SetActive(false);
            Destroy(visual);
        }

        private void InitializeStorageSlots()
        {
            if (storageSlots.Count == StorageSlotsPerPage) return;
            foreach (Transform child in blockStorageParent) RetireVisual(child.gameObject);
            storageSlots.Clear();
            ConfigureStorageLayout(blockStorageParent);
            for (int i = 0; i < StorageSlotsPerPage; i++)
            {
                GameObject slot = Instantiate(storageSlotPrefab, blockStorageParent);
                slot.name = $"StorageSlot_{i + 1}";
                storageSlots.Add(slot);
                Image frame = slot.GetComponent<Image>();
                PixelPerfectSlotFrame.Apply(frame);
            }
            InitializePagination();
        }

        public static void ConfigureStorageLayout(RectTransform storageRoot)
        {
            GridLayoutGroup layout = storageRoot.GetComponent<GridLayoutGroup>();
            if (layout != null)
            {
                layout.cellSize = Vector2.one * StorageSlotSize;
                layout.spacing = Vector2.one * 5f;
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = StorageColumns;
            }
            // 늘어나는 앵커와 고정 슬롯 크기가 충돌하지 않도록 상단 중앙을 기준으로 맞춘다.
            if (storageRoot.anchorMin != storageRoot.anchorMax)
            {
                storageRoot.anchorMin = storageRoot.anchorMax = new Vector2(0.5f, 1f);
                storageRoot.pivot = new Vector2(0.5f, 1f);
                storageRoot.anchoredPosition = Vector2.zero;
            }
            Vector2 spacing = layout != null ? layout.spacing : Vector2.one * 5f;
            float width = StorageSlotSize * StorageColumns + spacing.x * (StorageColumns - 1) +
                (layout != null ? layout.padding.horizontal : 0);
            float height = StorageSlotSize * StorageColumns + spacing.y * (StorageColumns - 1) +
                (layout != null ? layout.padding.vertical : 0);
            storageRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            storageRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void CreateBlockInStorage(RelicBlock blockData, GameObject slotObj)
        {
            slotObj.GetComponent<RelicSlotVisual>()?.SetRarity(blockData.SourceData.rarity);
            try
            {
                GameObject blockObj = container.Instantiate(draggableBlockPrefab, slotObj.transform);

                RelicBlockDraggable draggable = blockObj.GetComponent<RelicBlockDraggable>();

                if (draggable != null)
                {
                    draggable.isPlaced = false;
                    draggable.blockData = blockData;
                    draggable.BindStorageIcon(slotObj.transform.Find("Icon")?.GetComponent<Image>());
                    RectTransform blockRect = blockObj.GetComponent<RectTransform>();
                    blockRect.anchoredPosition = Vector2.zero;
                    blockRect.localScale = Vector3.one;
                    blockRect.sizeDelta = Vector2.one * StorageSlotSize;
                    draggable.BuildVisualFromShape();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RelicGridUI] 블록 생성 중 오류 발생: {e.Message}\n{e.StackTrace}");
            }
        }

        public GameObject CreateDismantleSlot(RelicBlock block, Transform parent)
        {
            GameObject slot = Instantiate(storageSlotPrefab, parent);
            PixelPerfectSlotFrame.Apply(slot.GetComponent<Image>());
            CreateBlockInStorage(block, slot);
            RelicBlockDraggable draggable = slot.GetComponentInChildren<RelicBlockDraggable>();
            if (draggable != null)
            {
                draggable.isDismantleStaged = true;
                GridLayoutGroup layout = parent.GetComponent<GridLayoutGroup>();
                if (layout != null) ((RectTransform)draggable.transform).sizeDelta = layout.cellSize;
            }
            return slot;
        }

        public void CloseDismantle() => dismantlePanel?.Close();

        private void CreateBlockOnGrid(RelicBlock blockData, Vector2Int position)
        {
            slotCells[position.y, position.x]?.SetRarity(blockData.SourceData.rarity);
            GameObject blockObj = container.Instantiate(draggableBlockPrefab, placedBlocksContainer);
            RelicBlockDraggable draggable = blockObj.GetComponent<RelicBlockDraggable>();

            draggable.isPlaced = true;
            draggable.gridPosition = position;
            draggable.blockData = blockData;
            draggable.BuildVisualFromShape();

            draggable.GetComponent<RectTransform>().sizeDelta = Vector2.one * EquippedSlotSize;
            draggable.GetComponent<RectTransform>().anchoredPosition = GetLocalPositionFromGridCell(position);
        }

        public void CancelActiveDrag()
        {
            if (rootCanvas == null) return;
            foreach (RelicBlockDraggable draggable in rootCanvas.rootCanvas.GetComponentsInChildren<RelicBlockDraggable>(true))
                draggable.CancelDrag();
        }

        public void ChangeStoragePage(int direction)
        {
            storagePage = Mathf.Clamp(storagePage + direction, 0, storagePageCount - 1);
            HandleRelicStateChanged();
        }

        public void SortStorage()
        {
            if (relicManager == null || !relicManager.SortStorageBlocks()) return;

            storagePage = 0;
            HandleRelicStateChanged();
        }

        private void InitializePagination()
        {
            if (paginationRoot == null || previousPageButton == null || nextPageButton == null ||
                storageSortButton == null || pageText == null)
            {
                Debug.LogError("[RelicGridUI] 하이라키의 보관소 페이지 UI를 Inspector에 연결해 주세요.", this);
                return;
            }

            previousPageButton.onClick.RemoveListener(ShowPreviousStoragePage);
            previousPageButton.onClick.AddListener(ShowPreviousStoragePage);
            nextPageButton.onClick.RemoveListener(ShowNextStoragePage);
            nextPageButton.onClick.AddListener(ShowNextStoragePage);
            storageSortButton.onClick.RemoveListener(SortStorage);
            storageSortButton.onClick.AddListener(SortStorage);
        }

        private void ShowPreviousStoragePage() => ChangeStoragePage(-1);
        private void ShowNextStoragePage() => ChangeStoragePage(1);

        private void UpdatePagination()
        {
            if (paginationRoot == null || previousPageButton == null || nextPageButton == null ||
                pageText == null) return;
            // 정렬 버튼은 보관함이 한 페이지일 때도 표시한다.
            previousPageButton.gameObject.SetActive(storagePageCount > 1);
            nextPageButton.gameObject.SetActive(storagePageCount > 1);
            pageText.gameObject.SetActive(storagePageCount > 1);
            previousPageButton.interactable = storagePage > 0;
            nextPageButton.interactable = storagePage + 1 < storagePageCount;
            pageText.text = $"{storagePage + 1}/{storagePageCount}";
        }

        public void OnCellPointerEnter(RelicSlotCell cell) => currentPointerOverCell = cell;
        public void OnCellPointerExit(RelicSlotCell cell) { if (currentPointerOverCell == cell) currentPointerOverCell = null; }

        public void GetDropTarget(PointerEventData eventData, out Vector2Int? gridPosition, out int? storageIndex)
        {
            gridPosition = null;
            storageIndex = null;
            GameObject target = eventData.pointerCurrentRaycast.gameObject;
            if (target == null) return;

            for (int i = 0; i < storageSlots.Count; i++)
            {
                if (target.transform.IsChildOf(storageSlots[i].transform))
                {
                    List<RelicBlock> blocks = relicManager.GetStorageBlocks().ToList();
                    List<RelicBlock> visibleBlocks = blocks
                        .Where(block => block != null && block.SourceData != null && block.SourceData.IsRuntimeAvailable &&
                            !relicManager.IsStagedForDismantle(block))
                        .ToList();
                    int visibleIndex = storagePage * StorageSlotsPerPage + i;
                    storageIndex = visibleIndex < visibleBlocks.Count
                        ? blocks.IndexOf(visibleBlocks[visibleIndex]) : blocks.Count;
                    return;
                }
            }

            RelicSlotCell cell = target.GetComponentInParent<RelicSlotCell>();
            if (cell != null && cell.transform.IsChildOf(gridRoot))
            {
                gridPosition = cell.GridPosition;
                return;
            }

            RelicBlockDraggable blockUI = target.GetComponentInParent<RelicBlockDraggable>();
            if (blockUI != null && blockUI.isPlaced && blockUI.transform.IsChildOf(placedBlocksContainer))
                gridPosition = blockUI.gridPosition;
        }

        public void ShowPlacementPreview(RelicBlock block, Vector2Int? gridPos)
        {
            ClearPreview();

            if (block == null || !gridPos.HasValue || slotCells == null || previewContainer == null) return;

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
                        RectTransform targetCellRect = slotCells[targetRow, targetCol].GetComponent<RectTransform>();
                        PreviewGizmo preview = GetPreviewGizmo(prefabToUse);
                        GameObject gizmo = preview.instance;
                        RectTransform gizmoRect = preview.rectTransform;
                        gizmoRect.anchoredPosition = previewContainer.InverseTransformPoint(targetCellRect.position);

                        Graphic graphic = preview.graphic;
                        if (graphic != null)
                        {
                            Color color;
                            if (isSilence && silenceGizmoPrefab == null) color = new Color(0.5f, 0, 0.5f);
                            else if (zone.type == InfluenceType.SynergyLink && synergyLinkGizmoPrefab == null) color = new Color(1f, 0.8f, 0f);
                            else color = preview.originalColor;

                            color.a = 0.5f;
                            graphic.color = color;
                        }

                        UpdatePreviewInfluenceAmountText(preview, zone);
                        gizmo.SetActive(true);
                    }
                }
            }
        }

        private PreviewGizmo GetPreviewGizmo(GameObject prefab)
        {
            if (!previewPools.TryGetValue(prefab, out Stack<PreviewGizmo> pool))
            {
                pool = new Stack<PreviewGizmo>();
                previewPools.Add(prefab, pool);
            }

            PreviewGizmo preview;
            if (pool.Count > 0)
            {
                preview = pool.Pop();
            }
            else
            {
                GameObject instance = Instantiate(prefab, previewContainer);
                Graphic graphic = instance.GetComponent<Graphic>();
                preview = new PreviewGizmo
                {
                    prefab = prefab,
                    instance = instance,
                    rectTransform = instance.GetComponent<RectTransform>(),
                    graphic = graphic,
                    originalColor = graphic != null ? graphic.color : Color.white
                };
                instance.SetActive(false);
            }

            activePreviewGizmos.Add(preview);
            return preview;
        }

        private static void UpdatePreviewInfluenceAmountText(PreviewGizmo preview, InfluenceZone zone)
        {
            string amountText = GetInfluenceAmountText(zone);
            if (string.IsNullOrEmpty(amountText))
            {
                if (preview.amountLabel != null) preview.amountLabel.gameObject.SetActive(false);
                return;
            }

            if (preview.amountLabel != null)
            {
                preview.amountLabel.text = amountText;
                preview.amountLabel.gameObject.SetActive(true);
                return;
            }

            GameObject textObject = new GameObject("InfluenceAmount", typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(preview.instance.transform, false);

            RectTransform textTransform = textObject.GetComponent<RectTransform>();
            textTransform.anchorMin = new Vector2(0.5f, 0.5f);
            textTransform.anchorMax = new Vector2(0.5f, 0.5f);
            textTransform.sizeDelta = new Vector2(90f, 90f);
            textTransform.anchoredPosition = Vector2.zero;

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = PreviewInfluenceAmountFontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.text = amountText;
            preview.amountLabel = label;
        }

        private static string GetInfluenceAmountText(InfluenceZone zone)
        {
            if (zone.type == InfluenceType.LevelUp)
            {
                return $"+{zone.GetLevelAmount()}";
            }

            if (zone.type == InfluenceType.LevelDown)
            {
                return $"-{zone.GetLevelAmount()}";
            }

            return null;
        }

        public void ClearPreview()
        {
            foreach (PreviewGizmo preview in activePreviewGizmos)
            {
                if (preview.instance == null) continue;
                preview.instance.SetActive(false);
                previewPools[preview.prefab].Push(preview);
            }
            activePreviewGizmos.Clear();
            if (slotCells == null) return;
            foreach (var cell in slotCells)
            {
                if (cell != null) cell.Highlight(false);
            }
        }

        public Vector2 GetLocalPositionFromGridCell(Vector2Int gridPos)
        {
            if (slotCells == null || gridPos.y < 0 || gridPos.y >= rows || gridPos.x < 0 || gridPos.x >= columns) return Vector2.zero;

            RectTransform targetCellRect = slotCells[gridPos.y, gridPos.x].GetComponent<RectTransform>();
            return placedBlocksContainer.InverseTransformPoint(targetCellRect.position);
        }
    }
}
