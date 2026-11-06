using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using Nytherion.GamePlay.Relics;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Core.Enums;
using Nytherion.Core.Utils;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Nytherion.UI.RelicBoard
{
    public class RelicTooltip : MonoBehaviour
{
    private const int EffectBadgesPerRow = 2;
    private const float EffectBadgeHeight = 40f;
    private const float EffectBadgeMinimumWidth = 56f;
    private const float EffectBadgeMaximumWidth = 176f;
    private const float EffectBadgeHorizontalSpacing = 8f;
    private const float EffectBadgeVerticalSpacing = 8f;
    private const int LevelIconCount = RelicBlock.MaxLevel;
    private const float LevelIconSpacing = 4f;

    public static RelicTooltip Instance { get; private set; }

    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Sprite levelIcon;
    [SerializeField] private Sprite levelEmptyIcon;
    [FormerlySerializedAs("effectTypeText")]
    [SerializeField] private TextMeshProUGUI setText;
    [SerializeField] private Image setBadgeBackground;
    [SerializeField] private RectTransform setBadgeContainer;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image itemImage;

    [Header("하이라키 효과 현황 툴팁")]
    [SerializeField] private RelicEffectTooltip setEffectTooltip;
    [SerializeField] private RelicEffectTooltip transcendenceEffectTooltip;

    [Header("Influence Grid")]
    [SerializeField] private Image[] influenceCells = new Image[9];
    [Tooltip("5×5 영향 범위에 사용할 배경 이미지입니다.")]
    [SerializeField] private Sprite expandedInfluenceGridBackground;
    [SerializeField] private Color centerColor = Color.yellow;
    [SerializeField] private Color levelUpColor = new Color(0.05f, 0.38f, 0.18f, 1f);
    [SerializeField] private Color levelDownColor = Color.red;
    [SerializeField] private Color neutralColor = new Color(50/255f, 50/255f, 50/255f, 200/255f);

    private const float InfluenceAmountFontSize = 22f;
    private const float InfluenceCellScale = 1.25f;
    private const float PreviousInfluenceCellScale = 1.125f;
    private const float InfluenceCellSpacing = 10f;
    private const float ExpandedInfluenceBackgroundAspect = 68f / 170f;
    private RectTransform rectTransform;
    private RectTransform influenceGridPanel;
    private Canvas canvas;
    private readonly Vector3[] tooltipCorners = new Vector3[4];
    private readonly List<EffectBadgeView> effectBadgeViews = new List<EffectBadgeView>();
    private readonly List<Image> levelIcons = new List<Image>();
    private Transform levelIconPanel;
    private Image[] expandedInfluenceCells;
    private readonly TextMeshProUGUI[] expandedInfluenceAmountTexts = new TextMeshProUGUI[25];
    private GameObject expandedInfluenceGrid;
    private bool missingInfluenceGridWarningIssued;
    private RectTransform influenceGridRect;
    private Image influenceGridBackground;
    private Sprite originalInfluenceGridBackground;
    private Vector2 originalInfluenceGridSize;
    private Vector2 originalInfluencePanelSize;
    private Vector2 originalInfluenceGridPosition;
    private Vector2 originalInfluencePanelPosition;
    private Vector2 descriptionBasePosition;
    private Vector2 descriptionBaseSize;
    private float nameBasePositionY;
    private RelicBlock currentBlock;
    private bool isLocalizationSubscribed;

    private void OnEnable()
    {
        LocalizationText.LanguageChanged += OnTemporaryLanguageChanged;

        if (LocalizationText.IsConfigured)
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            isLocalizationSubscribed = true;
        }
    }

    private void OnDisable()
    {
        LocalizationText.LanguageChanged -= OnTemporaryLanguageChanged;

        if (isLocalizationSubscribed)
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            isLocalizationSubscribed = false;
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rectTransform = tooltipPanel.GetComponent<RectTransform>();
        InitializeItemImage();
        InitializeLevelIcons();
        InitializeEffectBadgeViews();
        CacheEffectBadgeLayout();
        Hide();
    }

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>().rootCanvas;
    }

    private void LateUpdate()
    {
        if (!tooltipPanel.activeSelf || canvas == null) return;

        Vector2 mousePosition = Input.mousePosition;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 pivotPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, rectTransform.position);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        IncludeScreenBounds(rectTransform, uiCamera, ref min, ref max);

        if (influenceGridPanel == null) influenceGridPanel = FindInfluenceGridPanel();
        if (influenceGridPanel != null && influenceGridPanel.gameObject.activeInHierarchy)
        {
            IncludeScreenBounds(influenceGridPanel, uiCamera, ref min, ref max);
            if (influenceGridRect != null && influenceGridRect.gameObject.activeInHierarchy)
                IncludeScreenBounds(influenceGridRect, uiCamera, ref min, ref max);
            foreach (Image cell in influenceCells)
            {
                if (cell != null && cell.gameObject.activeInHierarchy && cell.transform.IsChildOf(influenceGridPanel))
                {
                    IncludeScreenBounds(cell.rectTransform, uiCamera, ref min, ref max);
                }
            }
        }

        Vector2 targetPivot = CalculateTooltipPosition(
            mousePosition, min - pivotPosition, max - pivotPosition, Screen.width, Screen.height);
        if (rectTransform.parent is RectTransform parentRect &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, targetPivot, uiCamera, out Vector3 worldPosition))
        {
            rectTransform.position = worldPosition;
        }
    }

    private static Vector2 CalculateTooltipPosition(
        Vector2 mousePosition, Vector2 minOffset, Vector2 maxOffset, float screenWidth, float screenHeight)
    {
        float width = maxOffset.x - minOffset.x;
        float height = maxOffset.y - minOffset.y;
        const float margin = 10f;
        const float cursorOffset = 15f;

        float left = mousePosition.x + cursorOffset;
        if (left + width > screenWidth - margin) left = mousePosition.x - cursorOffset - width;
        left = width <= screenWidth - margin * 2f
            ? Mathf.Clamp(left, margin, screenWidth - margin - width)
            : margin;

        float top = mousePosition.y - cursorOffset;
        if (top - height < margin) top = mousePosition.y + cursorOffset + height;
        top = height <= screenHeight - margin * 2f
            ? Mathf.Clamp(top, margin + height, screenHeight - margin)
            : screenHeight - margin;

        return new Vector2(left - minOffset.x, top - maxOffset.y);
    }

    private RectTransform FindInfluenceGridPanel()
    {
        RectTransform namedPanel = tooltipPanel.transform.Find("InfluenceGridPanel") as RectTransform;
        if (namedPanel != null) return namedPanel;

        foreach (Image cell in influenceCells)
        {
            if (cell == null) continue;
            Transform child = cell.transform;
            while (child.parent != null && child.parent != tooltipPanel.transform) child = child.parent;
            if (child.parent == tooltipPanel.transform) return child as RectTransform;
        }

        return null;
    }

    private void IncludeScreenBounds(RectTransform target, Camera uiCamera, ref Vector2 min, ref Vector2 max)
    {
        target.GetWorldCorners(tooltipCorners);
        foreach (Vector3 corner in tooltipCorners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(uiCamera, corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
    }

    public void Show(RelicBlock block)
    {
        if (block == null) return;

        setEffectTooltip?.Hide();
        transcendenceEffectTooltip?.Hide();
        currentBlock = block;
        if (itemImage != null)
        {
            itemImage.sprite = block.SourceData.Image;
            itemImage.enabled = itemImage.sprite != null;
            itemImage.gameObject.SetActive(itemImage.sprite != null);
        }

        SetInfluenceGridVisible(true);

        nameText.text = block.SourceData.DisplayName;
        nameText.color = block.SourceData.rarity switch
        {
            Rarity.Common => new Color32(170, 170, 170, 255),
            Rarity.Uncommon => new Color32(39, 232, 106, 255),
            Rarity.Rare => new Color32(21, 154, 232, 255),
            Rarity.Epic => new Color32(181, 65, 238, 255),
            Rarity.Legendary => new Color32(255, 196, 0, 255),
            _ => Color.white
        };
        tooltipPanel.SetActive(true);
        UpdateLevelIcons(block.SourceData.level);
        bool hasSetBadge = UpdateEffectBadges(block);
        Vector2 namePosition = nameText.rectTransform.anchoredPosition;
        namePosition.y = hasSetBadge ? nameBasePositionY : -95f;
        nameText.rectTransform.anchoredPosition = namePosition;
        descriptionText.color = Color.white;
        descriptionText.text = block.SourceData.Description;

        UpdateInfluenceGrid(block);

    }

    public void ShowStatus(string title, string description, bool isTranscendence = false,
        Color? titleColor = null, Sprite icon = null, IReadOnlyList<Sprite> requirementIcons = null)
    {
        Hide();
        RelicEffectTooltip effectTooltip = isTranscendence
            ? transcendenceEffectTooltip
            : setEffectTooltip;
        if (effectTooltip == null)
        {
            Debug.LogWarning("[RelicTooltip] 하이라키의 세트/초월 효과 툴팁 참조가 비어 있습니다.");
            return;
        }
        effectTooltip.Show(title, description, titleColor, icon, requirementIcons);
    }

    public void Hide()
    {
        currentBlock = null;
        tooltipPanel.SetActive(false);
        setEffectTooltip?.Hide();
        transcendenceEffectTooltip?.Hide();
    }

    private void OnLocaleChanged(Locale _)
    {
        if (tooltipPanel.activeSelf && currentBlock != null)
        {
            Show(currentBlock);
        }
    }

    private void OnTemporaryLanguageChanged()
    {
        OnLocaleChanged(null);
    }

    private void InitializeItemImage()
    {
        Transform background = tooltipPanel.transform.Find("ItemImageBackground");
        if (background != null)
        {
            Image backgroundImage = background.GetComponent<Image>();
            if (backgroundImage != null) backgroundImage.enabled = false;
        }

        if (itemImage == null)
        {
            if (background == null) return;

            Transform icon = background.Find("ItemImage");
            if (icon != null)
            {
                itemImage = icon.GetComponent<Image>();
            }
            else
            {
                GameObject iconObject = new GameObject("ItemImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.layer = background.gameObject.layer;
                iconObject.transform.SetParent(background, false);
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(15f, 15f);
                iconRect.offsetMax = new Vector2(-15f, -15f);
                itemImage = iconObject.GetComponent<Image>();
            }
        }

        if (itemImage != null)
        {
            itemImage.preserveAspect = true;
            itemImage.raycastTarget = false;
        }
    }

    private void InitializeLevelIcons()
    {
        if (levelText != null) levelText.gameObject.SetActive(false);
        if (levelEmptyIcon == null)
        {
            Debug.LogWarning("[RelicTooltip] Level Empty Icon 스프라이트가 지정되지 않았습니다.");
        }

        Image template = null;
        foreach (Transform child in tooltipPanel.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "LevelIconPanel") levelIconPanel = child;
            else if (child.name == "LevelIcon" && template == null) template = child.GetComponent<Image>();
        }

        if (levelIconPanel == null)
        {
            Debug.LogWarning("[RelicTooltip] LevelIconPanel 오브젝트를 찾을 수 없습니다.");
            return;
        }

        if (template != null)
        {
            if (levelIcon == null) levelIcon = template.sprite;
            if (template.transform.parent != levelIconPanel)
            {
                template.gameObject.SetActive(false);
                template = Instantiate(template, levelIconPanel, false);
                template.name = "LevelIcon_1";
            }
        }
        else
        {
            GameObject iconObject = new GameObject("LevelIcon_1", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = levelIconPanel.gameObject.layer;
            iconObject.transform.SetParent(levelIconPanel, false);
            template = iconObject.GetComponent<Image>();
            Sprite referenceSprite = levelIcon != null ? levelIcon : levelEmptyIcon;
            float width = referenceSprite != null ? referenceSprite.rect.width : 16f;
            float height = referenceSprite != null ? referenceSprite.rect.height : 16f;
            template.rectTransform.sizeDelta = new Vector2(width, height);
        }

        template.raycastTarget = false;
        LayoutElement layoutElement = template.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = template.gameObject.AddComponent<LayoutElement>();
        layoutElement.enabled = true;
        layoutElement.ignoreLayout = true;
        levelIcons.Add(template);
        template.gameObject.SetActive(false);
    }

    private void UpdateLevelIcons(int level)
    {
        if (levelIcons.Count == 0) return;

        while (levelIcons.Count < LevelIconCount)
        {
            Image template = levelIcons[0];
            Image icon = Instantiate(template, levelIconPanel, false);
            icon.name = $"LevelIcon_{levelIcons.Count + 1}";
            levelIcons.Add(icon);
        }

        int filledCount = Mathf.Clamp(level, 0, LevelIconCount);
        RectTransform templateRect = levelIcons[0].rectTransform;
        Sprite referenceSprite = levelIcon != null ? levelIcon : levelEmptyIcon;
        Vector2 iconSize = templateRect.rect.size;
        if (iconSize.x <= 0f) iconSize.x = referenceSprite != null ? referenceSprite.rect.width : 16f;
        if (iconSize.y <= 0f) iconSize.y = referenceSprite != null ? referenceSprite.rect.height : 16f;
        float iconWidth = iconSize.x * Mathf.Abs(templateRect.localScale.x);
        for (int i = 0; i < levelIcons.Count; i++)
        {
            Image icon = levelIcons[i];
            icon.sprite = i < filledCount ? levelIcon : levelEmptyIcon;
            icon.enabled = true;
            icon.gameObject.SetActive(true);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = iconSize;
            iconRect.anchoredPosition = new Vector2(iconWidth * 0.5f + i * (iconWidth + LevelIconSpacing), 0f);
        }
    }

    private void UpdateInfluenceGrid(RelicBlock block)
    {
        List<InfluenceZone> zones = block.GetRotatedInfluenceZones();
        int minX = 0, maxX = 0, minY = 0, maxY = 0;
        foreach (InfluenceZone zone in zones)
        {
            minX = Mathf.Min(minX, zone.offset.x);
            maxX = Mathf.Max(maxX, zone.offset.x);
            minY = Mathf.Min(minY, zone.offset.y);
            maxY = Mathf.Max(maxY, zone.offset.y);
        }

        const int size = 5;
        EnsureExpandedInfluenceGrid();
        if (expandedInfluenceGrid == null || expandedInfluenceCells == null)
        {
            if (!missingInfluenceGridWarningIssued)
            {
                Debug.LogWarning("유물 툴팁의 영향 범위 셀 참조가 없어 5×5 격자를 표시할 수 없습니다.", this);
                missingInfluenceGridWarningIssued = true;
            }
            SetInfluenceGridVisible(false);
            return;
        }
        missingInfluenceGridWarningIssued = false;
        expandedInfluenceGrid.SetActive(true);
        SetInfluenceGridVisible(false);
        ResizeInfluenceGrid(size);

        Image[] cells = expandedInfluenceCells;
        TextMeshProUGUI[] amountTexts = expandedInfluenceAmountTexts;
        // 외곽 안에 유물과 모든 영향 칸을 포함하면서 원점을 가능한 가운데에 둡니다.
        int left = Mathf.Clamp(-(size / 2), maxX - size + 1, minX);
        int top = Mathf.Clamp(size / 2, maxY, minY + size - 1);

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null) cells[i].color = neutralColor;
            SetInfluenceAmountText(cells, amountTexts, i, null);
        }

        int centerIndex = top * size - left;
        if (centerIndex >= 0 && centerIndex < cells.Length && cells[centerIndex] != null)
            cells[centerIndex].color = centerColor;

        foreach (InfluenceZone zone in zones)
        {
            int row = top - zone.offset.y;
            int column = zone.offset.x - left;
            if (row < 0 || row >= size || column < 0 || column >= size) continue;
            int index = row * size + column;
            if (index >= cells.Length || cells[index] == null) continue;

            if (zone.type == InfluenceType.LevelUp)
            {
                cells[index].color = levelUpColor;
                SetInfluenceAmountText(cells, amountTexts, index, $"+{zone.GetLevelAmount()}");
            }
            else if (zone.type == InfluenceType.LevelDown)
            {
                cells[index].color = levelDownColor;
                SetInfluenceAmountText(cells, amountTexts, index, $"-{zone.GetLevelAmount()}");
            }
            else if (zone.type == InfluenceType.Silence)
            {
                cells[index].color = new Color(0.5f, 0f, 0.5f, 1f);
            }
        }
    }

    private void EnsureExpandedInfluenceGrid()
    {
        if (expandedInfluenceGrid != null || influenceCells == null) return;
        Image template = null;
        foreach (Image cell in influenceCells)
        {
            if (cell == null) continue;
            template = cell;
            break;
        }
        if (template == null) return;
        RectTransform parent = template.transform.parent as RectTransform;
        if (parent == null) return;
        influenceGridRect = parent;
        originalInfluenceGridSize = parent.rect.size;
        originalInfluenceGridPosition = parent.anchoredPosition;
        if (influenceGridPanel == null) influenceGridPanel = FindInfluenceGridPanel();
        if (influenceGridPanel != null)
        {
            originalInfluencePanelSize = influenceGridPanel.rect.size;
            originalInfluencePanelPosition = influenceGridPanel.anchoredPosition;
            if (influenceGridPanel != influenceGridRect)
            {
                influenceGridBackground = influenceGridPanel.GetComponent<Image>();
                if (influenceGridBackground != null)
                    originalInfluenceGridBackground = influenceGridBackground.sprite;
            }
        }

        expandedInfluenceGrid = new GameObject("ExpandedInfluenceGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
        expandedInfluenceGrid.layer = parent.gameObject.layer;
        RectTransform gridRect = expandedInfluenceGrid.GetComponent<RectTransform>();
        gridRect.SetParent(parent, false);
        gridRect.anchorMin = Vector2.zero;
        gridRect.anchorMax = Vector2.one;
        gridRect.offsetMin = Vector2.zero;
        gridRect.offsetMax = Vector2.zero;
        expandedInfluenceGrid.GetComponent<LayoutElement>().ignoreLayout = true;

        GridLayoutGroup layout = expandedInfluenceGrid.GetComponent<GridLayoutGroup>();
        GridLayoutGroup originalLayout = parent.GetComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = new Vector2(InfluenceCellSpacing, InfluenceCellSpacing);
        if (originalLayout != null)
        {
            originalLayout.cellSize *= InfluenceCellScale;
            originalLayout.spacing = layout.spacing;
            layout.cellSize = originalLayout.cellSize;
            layout.padding = new RectOffset(originalLayout.padding.left, originalLayout.padding.right,
                originalLayout.padding.top, originalLayout.padding.bottom);
        }
        else
        {
            layout.cellSize = template.rectTransform.rect.size * InfluenceCellScale;
            foreach (Image cell in influenceCells)
            {
                if (cell != null) cell.rectTransform.sizeDelta = layout.cellSize;
            }
        }

        expandedInfluenceCells = new Image[25];
        for (int i = 0; i < expandedInfluenceCells.Length; i++)
        {
            GameObject cellObject = new GameObject($"InfluenceCell{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cellObject.layer = gridRect.gameObject.layer;
            cellObject.transform.SetParent(gridRect, false);
            Image cell = cellObject.GetComponent<Image>();
            cell.sprite = template.sprite;
            cell.material = template.material;
            cell.type = template.type;
            cell.preserveAspect = template.preserveAspect;
            cell.raycastTarget = false;
            expandedInfluenceCells[i] = cell;
        }
    }

    private void ResizeInfluenceGrid(int size)
    {
        GridLayoutGroup layout = expandedInfluenceGrid.GetComponent<GridLayoutGroup>();
        float width = layout.padding.horizontal + size * layout.cellSize.x + (size - 1) * layout.spacing.x;
        float height = layout.padding.vertical + size * layout.cellSize.y + (size - 1) * layout.spacing.y;
        Vector2 gridSize = new Vector2(Mathf.Max(originalInfluenceGridSize.x, width), Mathf.Max(originalInfluenceGridSize.y, height));
        float heightIncrease = gridSize.y - originalInfluenceGridSize.y;

        if (influenceGridPanel != null && influenceGridPanel != influenceGridRect)
        {
            float panelWidth = Mathf.Max(originalInfluencePanelSize.x, gridSize.x);
            float panelHeight = originalInfluencePanelSize.y + heightIncrease;
            bool useExpandedBackground = size == 5 && influenceGridBackground != null && expandedInfluenceGridBackground != null;
            if (influenceGridBackground != null)
            {
                influenceGridBackground.sprite = useExpandedBackground
                    ? expandedInfluenceGridBackground
                    : originalInfluenceGridBackground;
            }
            if (useExpandedBackground && expandedInfluenceGridBackground.rect.width > 0f)
            {
                float imageHeight = panelWidth * expandedInfluenceGridBackground.rect.height / expandedInfluenceGridBackground.rect.width;
                panelHeight = Mathf.Max(panelHeight, imageHeight);
            }
            else if (size == 5 && influenceGridBackground != null)
            {
                panelHeight = Mathf.Max(panelHeight, panelWidth * ExpandedInfluenceBackgroundAspect);
            }
            if (originalInfluenceGridBackground != null && originalInfluenceGridBackground.rect.width > 0f)
            {
                float pixelScale = panelWidth / originalInfluenceGridBackground.rect.width;
                panelHeight = Mathf.Ceil(panelHeight / pixelScale) * pixelScale;
            }
            float panelHeightIncrease = panelHeight - originalInfluencePanelSize.y;
            influenceGridPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
            influenceGridPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
            influenceGridPanel.anchoredPosition = originalInfluencePanelPosition - new Vector2(0f, panelHeightIncrease * (1f - influenceGridPanel.pivot.y));
        }

        influenceGridRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, gridSize.x);
        influenceGridRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, gridSize.y);
        // 별도 배경이 없는 격자는 윗변을 고정해 설명 영역과 겹치지 않게 합니다.
        influenceGridRect.anchoredPosition = influenceGridPanel == influenceGridRect
            ? originalInfluenceGridPosition - new Vector2(0f, heightIncrease * (1f - influenceGridRect.pivot.y))
            : originalInfluenceGridPosition;
    }

    private static void SetInfluenceAmountText(Image[] cells, TextMeshProUGUI[] amountTexts, int index, string value)
    {
        if (index < 0 || index >= cells.Length || index >= amountTexts.Length || cells[index] == null) return;

        TextMeshProUGUI amountText = amountTexts[index];
        if (amountText == null && !string.IsNullOrEmpty(value))
        {
            amountText = CreateInfluenceAmountText(cells[index].transform);
            amountText.enableAutoSizing = true;
            amountText.fontSizeMin = 8f;
            amountText.fontSizeMax = InfluenceAmountFontSize;
            amountTexts[index] = amountText;
        }

        if (amountText == null) return;
        bool hasValue = !string.IsNullOrEmpty(value);
        amountText.gameObject.SetActive(hasValue);
        if (hasValue) amountText.text = value;
    }

    private static TextMeshProUGUI CreateInfluenceAmountText(Transform parent)
    {
        GameObject textObject = new GameObject("InfluenceAmount", typeof(RectTransform), typeof(CanvasRenderer));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        // 자동 크기 조정의 기준 영역을 이전 칸 크기로 유지합니다.
        float inset = (1f - PreviousInfluenceCellScale / InfluenceCellScale) * 0.5f;
        textTransform.anchorMin = new Vector2(inset, inset);
        textTransform.anchorMax = new Vector2(1f - inset, 1f - inset);
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI amountText = textObject.AddComponent<TextMeshProUGUI>();
        amountText.font = TMP_Settings.defaultFontAsset;
        amountText.fontSize = InfluenceAmountFontSize;
        amountText.fontStyle = FontStyles.Bold;
        amountText.alignment = TextAlignmentOptions.Center;
        amountText.color = Color.white;
        amountText.raycastTarget = false;
        amountText.enableWordWrapping = false;
        return amountText;
    }

    private void SetInfluenceGridVisible(bool isVisible)
    {
        if (influenceCells == null) return;
        foreach (Image cell in influenceCells)
        {
            if (cell != null)
            {
                cell.gameObject.SetActive(isVisible);
            }
        }
    }

    private void InitializeEffectBadgeViews()
    {
        effectBadgeViews.Clear();
        if (setText != null && setBadgeBackground != null)
        {
            effectBadgeViews.Add(new EffectBadgeView(
                setBadgeBackground.gameObject,
                setBadgeBackground,
                setText));
        }
    }

    private bool UpdateEffectBadges(RelicBlock block)
    {
        if (setText == null || effectBadgeViews.Count == 0) return false;

        List<EffectBadgeData> badges = new List<EffectBadgeData>();
        HashSet<string> addedNames = new HashSet<string>();
        var setBonusData = block.SourceData.synergySetBonusData;

        if (setBonusData != null)
        {
            if (!string.IsNullOrEmpty(setBonusData.DisplayName) &&
                addedNames.Add(setBonusData.DisplayName))
            {
                badges.Add(new EffectBadgeData(
                    setBonusData.DisplayName,
                    setBonusData.badgeBackgroundColor,
                    setBonusData.badgeTextColor));
            }

            if (setBonusData.linkedTranscendenceEffects != null)
            {
                foreach (var transcendenceData in setBonusData.linkedTranscendenceEffects)
                {
                    if (transcendenceData == null ||
                        string.IsNullOrEmpty(transcendenceData.DisplayName) ||
                        !addedNames.Add(transcendenceData.DisplayName))
                    {
                        continue;
                    }

                    badges.Add(new EffectBadgeData(
                        transcendenceData.DisplayName,
                        transcendenceData.badgeBackgroundColor,
                        transcendenceData.badgeTextColor));
                }
            }
        }

        BindEffectBadges(badges);
        return badges.Count > 0;
    }

    private void BindEffectBadges(IReadOnlyList<EffectBadgeData> badges)
    {
        if (effectBadgeViews.Count == 0) return;

        if (setBadgeContainer == null)
        {
            EffectBadgeView fallbackView = effectBadgeViews[0];
            List<string> labels = new List<string>();
            foreach (EffectBadgeData badge in badges)
            {
                labels.Add(badge.Label);
            }

            fallbackView.Text.text = string.Join(", ", labels);
            if (badges.Count > 0)
            {
                fallbackView.Background.color = badges[0].BackgroundColor;
                fallbackView.Text.color = badges[0].TextColor;
            }
            fallbackView.Root.SetActive(badges.Count > 0);
            return;
        }

        GameObject template = effectBadgeViews[0].Root;
        while (effectBadgeViews.Count < badges.Count)
        {
            GameObject badgeObject = Instantiate(template, setBadgeContainer, false);
            badgeObject.name = $"SetBadge_{effectBadgeViews.Count + 1}";

            Image background = badgeObject.GetComponent<Image>();
            TextMeshProUGUI text = badgeObject.GetComponentInChildren<TextMeshProUGUI>(true);
            effectBadgeViews.Add(new EffectBadgeView(badgeObject, background, text));
        }

        for (int i = 0; i < effectBadgeViews.Count; i++)
        {
            bool isVisible = i < badges.Count;
            EffectBadgeView view = effectBadgeViews[i];
            view.Root.SetActive(isVisible);
            if (!isVisible) continue;

            EffectBadgeData badge = badges[i];
            view.Background.color = badge.BackgroundColor;
            view.Text.color = badge.TextColor;
            view.Text.text = badge.Label;
        }

        setBadgeContainer.gameObject.SetActive(badges.Count > 0);
        if (badges.Count > 0)
        {
            LayoutEffectBadges(badges.Count);
        }

        UpdateContentPositions(badges.Count);
    }

    private void SetEffectBadgesVisible(bool isVisible)
    {
        if (setBadgeContainer != null)
        {
            setBadgeContainer.gameObject.SetActive(isVisible);
            if (!isVisible)
            {
                UpdateContentPositions(0);
            }
        }
        else if (setBadgeBackground != null)
        {
            setBadgeBackground.gameObject.SetActive(isVisible);
        }
    }

    private void CacheEffectBadgeLayout()
    {
        if (nameText != null)
        {
            nameBasePositionY = nameText.rectTransform.anchoredPosition.y;
        }

        if (setBadgeContainer != null)
        {
            LayoutGroup previousLayoutGroup = setBadgeContainer.GetComponent<LayoutGroup>();
            if (previousLayoutGroup != null)
            {
                previousLayoutGroup.enabled = false;
            }

            ContentSizeFitter previousSizeFitter = setBadgeContainer.GetComponent<ContentSizeFitter>();
            if (previousSizeFitter != null)
            {
                previousSizeFitter.enabled = false;
            }
        }

        if (descriptionText != null)
        {
            descriptionBasePosition = descriptionText.rectTransform.anchoredPosition;
            descriptionBaseSize = descriptionText.rectTransform.sizeDelta;
        }
    }

    private void LayoutEffectBadges(int badgeCount)
    {
        float[] badgeWidths = new float[badgeCount];
        for (int i = 0; i < badgeCount; i++)
        {
            EffectBadgeView view = effectBadgeViews[i];
            HorizontalLayoutGroup badgeLayout = view.Root.GetComponent<HorizontalLayoutGroup>();
            float horizontalPadding = badgeLayout != null ? badgeLayout.padding.horizontal : 0f;
            float preferredTextWidth = view.Text.GetPreferredValues(view.Text.text).x;
            badgeWidths[i] = Mathf.Clamp(
                Mathf.Ceil(preferredTextWidth + horizontalPadding),
                EffectBadgeMinimumWidth,
                EffectBadgeMaximumWidth);
        }

        int rowCount = Mathf.CeilToInt(badgeCount / (float)EffectBadgesPerRow);
        float widestRow = 0f;

        for (int row = 0; row < rowCount; row++)
        {
            int firstIndex = row * EffectBadgesPerRow;
            int rowItemCount = Mathf.Min(EffectBadgesPerRow, badgeCount - firstIndex);
            float rowWidth = 0f;

            for (int column = 0; column < rowItemCount; column++)
            {
                rowWidth += badgeWidths[firstIndex + column];
            }
            rowWidth += EffectBadgeHorizontalSpacing * (rowItemCount - 1);
            widestRow = Mathf.Max(widestRow, rowWidth);

            float currentX = -rowWidth * 0.5f;
            for (int column = 0; column < rowItemCount; column++)
            {
                int badgeIndex = firstIndex + column;
                float badgeWidth = badgeWidths[badgeIndex];
                RectTransform badgeRectTransform = effectBadgeViews[badgeIndex].RectTransform;

                badgeRectTransform.anchorMin = new Vector2(0.5f, 1f);
                badgeRectTransform.anchorMax = new Vector2(0.5f, 1f);
                badgeRectTransform.pivot = new Vector2(0.5f, 1f);
                badgeRectTransform.sizeDelta = new Vector2(badgeWidth, EffectBadgeHeight);
                badgeRectTransform.anchoredPosition = new Vector2(
                    currentX + badgeWidth * 0.5f,
                    -row * (EffectBadgeHeight + EffectBadgeVerticalSpacing));

                LayoutRebuilder.ForceRebuildLayoutImmediate(badgeRectTransform);
                currentX += badgeWidth + EffectBadgeHorizontalSpacing;
            }
        }

        float containerHeight = rowCount * EffectBadgeHeight +
                                Mathf.Max(0, rowCount - 1) * EffectBadgeVerticalSpacing;
        setBadgeContainer.sizeDelta = new Vector2(widestRow, containerHeight);
    }

    private void UpdateContentPositions(int badgeCount)
    {
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(badgeCount / (float)EffectBadgesPerRow));
        float additionalHeight = (rowCount - 1) *
                                 (EffectBadgeHeight + EffectBadgeVerticalSpacing);

        if (descriptionText != null)
        {
            float targetHeight = Mathf.Max(
                EffectBadgeHeight,
                descriptionBaseSize.y - additionalHeight);
            float reducedHeight = descriptionBaseSize.y - targetHeight;

            descriptionText.rectTransform.sizeDelta = new Vector2(
                descriptionBaseSize.x,
                targetHeight);
            descriptionText.rectTransform.anchoredPosition =
                descriptionBasePosition + Vector2.down * (reducedHeight * 0.5f);
        }
    }

    private sealed class EffectBadgeView
    {
        public GameObject Root { get; }
        public RectTransform RectTransform { get; }
        public Image Background { get; }
        public TextMeshProUGUI Text { get; }

        public EffectBadgeView(GameObject root, Image background, TextMeshProUGUI text)
        {
            Root = root;
            RectTransform = root.transform as RectTransform;
            Background = background;
            Text = text;
        }
    }

    private readonly struct EffectBadgeData
    {
        public string Label { get; }
        public Color BackgroundColor { get; }
        public Color TextColor { get; }

        public EffectBadgeData(string label, Color backgroundColor, Color textColor)
        {
            Label = label;
            BackgroundColor = backgroundColor;
            TextColor = textColor;
        }
    }
}
}
