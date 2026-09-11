using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using Nytherion.GamePlay.Relics;
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

    public static RelicTooltip Instance { get; private set; }

    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [FormerlySerializedAs("effectTypeText")]
    [SerializeField] private TextMeshProUGUI setText;
    [SerializeField] private Image setBadgeBackground;
    [SerializeField] private RectTransform setBadgeContainer;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Influence Grid")]
    [SerializeField] private Image[] influenceCells = new Image[9];
    [SerializeField] private Color centerColor = Color.yellow;
    [SerializeField] private Color levelUpColor = new Color(0.05f, 0.38f, 0.18f, 1f);
    [SerializeField] private Color levelDownColor = Color.red;
    [SerializeField] private Color neutralColor = new Color(50/255f, 50/255f, 50/255f, 200/255f);

    private const float InfluenceAmountFontSize = 22f;
    private RectTransform rectTransform;
    private Canvas canvas;
    private readonly List<EffectBadgeView> effectBadgeViews = new List<EffectBadgeView>();
    private readonly TextMeshProUGUI[] influenceAmountTexts = new TextMeshProUGUI[9];
    private Vector2 descriptionBasePosition;
    private Vector2 descriptionBaseSize;
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
        
        // Pivot을 (0, 1) [좌측 상단]으로 고정하여 계산을 단순화합니다.
        rectTransform.pivot = new Vector2(0, 1);
        
        float width = rectTransform.rect.width * canvas.scaleFactor;
        float height = rectTransform.rect.height * canvas.scaleFactor;
        
        float margin = 10f;
        float cursorOffset = 15f;
        
        // 기본 위치: 마우스 우측 하단
        float targetX = mousePosition.x + cursorOffset;
        float targetY = mousePosition.y - cursorOffset;
        
        // 화면 오른쪽 경계를 넘어가는 경우 마우스 좌측으로 배치
        if (targetX + width > Screen.width - margin)
        {
            targetX = mousePosition.x - width - cursorOffset;
        }
        // 마우스 좌측으로 배치했을 때도 화면 왼쪽 경계를 넘어가는 경우 화면 좌측 경계에 맞춤
        if (targetX < margin)
        {
            targetX = margin;
        }
        
        // 화면 아래쪽 경계를 넘어가는 경우 마우스 상단으로 배치
        if (targetY - height < margin)
        {
            targetY = mousePosition.y + height + cursorOffset;
        }
        // 마우스 상단으로 배치했을 때도 화면 위쪽 경계를 넘어가는 경우 화면 위쪽 경계에 맞춤
        if (targetY > Screen.height - margin)
        {
            targetY = Screen.height - margin;
        }
        
        rectTransform.position = new Vector2(targetX, targetY);
    }

    public void Show(RelicBlock block)
    {
        if (block == null) return;

        currentBlock = block;

        SetInfluenceGridVisible(true);

        nameText.text = block.SourceData.DisplayName;
        if (levelText != null)
        {
            levelText.text = $"Lv. {block.SourceData.level}";
            levelText.gameObject.SetActive(true);
        }
        UpdateEffectBadges(block);
        descriptionText.color = Color.white;
        descriptionText.text = block.SourceData.Description;

        UpdateInfluenceGrid(block);

        tooltipPanel.SetActive(true);
    }

    public void ShowStatus(string title, string description)
    {
        currentBlock = null;
        SetInfluenceGridVisible(false);
        nameText.text = title;
        if (levelText != null)
        {
            levelText.gameObject.SetActive(false);
        }
        SetEffectBadgesVisible(false);
        descriptionText.color = Color.white;
        descriptionText.text = description;
        tooltipPanel.SetActive(true);
    }

    public void Hide()
    {
        currentBlock = null;
        tooltipPanel.SetActive(false);
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

    private void UpdateInfluenceGrid(RelicBlock block)
    {
        for (int i = 0; i < influenceCells.Length; i++)
        {
            Image cell = influenceCells[i];
            if (cell != null)
            {
                cell.color = neutralColor;
            }

            SetInfluenceAmountText(i, null);
        }

        if (influenceCells.Length > 4 && influenceCells[4] != null)
        {
            influenceCells[4].color = centerColor;
        }

        foreach (var zone in block.GetRotatedInfluenceZones())
        {
            int rowIndex = zone.offset.y * -1 + 1; 
            int colIndex = 1 + zone.offset.x; 
            int index = (rowIndex * 3) + colIndex;

            if (index >= 0 && index < influenceCells.Length && influenceCells[index] != null)
            {
                if (zone.type == InfluenceType.LevelUp)
                {
                    influenceCells[index].color = levelUpColor;
                    SetInfluenceAmountText(index, $"+{zone.GetLevelAmount()}");
                }
                else if (zone.type == InfluenceType.LevelDown)
                {
                    influenceCells[index].color = levelDownColor;
                    SetInfluenceAmountText(index, $"-{zone.GetLevelAmount()}");
                }
            }
        }
    }

    private void SetInfluenceAmountText(int index, string value)
    {
        if (index < 0 || index >= influenceCells.Length || influenceCells[index] == null) return;

        TextMeshProUGUI amountText = influenceAmountTexts[index];
        if (amountText == null)
        {
            amountText = CreateInfluenceAmountText(influenceCells[index].transform);
            influenceAmountTexts[index] = amountText;
        }

        bool hasValue = !string.IsNullOrEmpty(value);
        amountText.gameObject.SetActive(hasValue);
        if (hasValue)
        {
            amountText.text = value;
        }
    }

    private static TextMeshProUGUI CreateInfluenceAmountText(Transform parent)
    {
        GameObject textObject = new GameObject("InfluenceAmount", typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
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

    private void UpdateEffectBadges(RelicBlock block)
    {
        if (setText == null) return;

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
