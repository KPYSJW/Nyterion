using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Nytherion.UI.RelicBoard
{
    /// <summary>
    /// 세트와 초월 효과를 각각 전용 프리팹으로 표시한다.
    /// </summary>
    public sealed class RelicEffectStatusUI : MonoBehaviour
    {
        private static readonly Color[] SetTierNameColors =
        {
            new Color32(39, 232, 106, 255),
            new Color32(21, 154, 232, 255),
            new Color32(181, 65, 238, 255),
            new Color32(255, 196, 0, 255)
        };

        private sealed class SetStatusDisplayData
        {
            public Sprite Icon { get; }
            public string Label { get; }
            public string TooltipTitle { get; }
            public string TooltipBody { get; }
            public Color NameColor { get; }

            public SetStatusDisplayData(
                Sprite icon,
                string label,
                string tooltipTitle,
                string tooltipBody,
                Color nameColor)
            {
                Icon = icon;
                Label = label;
                TooltipTitle = tooltipTitle;
                TooltipBody = tooltipBody;
                NameColor = nameColor;
            }
        }

        private sealed class TranscendenceStatusDisplayData
        {
            public Sprite Icon { get; }
            public string Label { get; }
            public string TooltipTitle { get; }
            public string TooltipBody { get; }
            public Color NameColor { get; }
            public IReadOnlyList<Sprite> RequirementIcons { get; }

            public TranscendenceStatusDisplayData(
                Sprite icon,
                string label,
                string tooltipTitle,
                string tooltipBody,
                Color nameColor,
                IReadOnlyList<Sprite> requirementIcons)
            {
                Icon = icon;
                Label = label;
                TooltipTitle = tooltipTitle;
                TooltipBody = tooltipBody;
                NameColor = nameColor;
                RequirementIcons = requirementIcons;
            }
        }

        private readonly List<RelicSetEffectStatusEntryUI> setEntries =
            new List<RelicSetEffectStatusEntryUI>();
        private readonly List<RelicTranscendenceEffectStatusEntryUI> transcendenceEntries =
            new List<RelicTranscendenceEffectStatusEntryUI>();

        private RelicManager relicManager;
        private RectTransform setStatusPanel;
        private RectTransform transcendenceStatusPanel;
        private GameObject setEntryPrefab;
        private GameObject transcendenceEntryPrefab;
        private RelicTooltip tooltip;
        private bool initialized;
        private bool isLocalizationSubscribed;

        public void Initialize(
            RelicManager manager,
            RectTransform assignedSetStatusPanel,
            RectTransform assignedTranscendenceStatusPanel,
            GameObject assignedSetEntryPrefab,
            GameObject assignedTranscendenceEntryPrefab,
            RelicTooltip assignedTooltip)
        {
            if (initialized) return;

            if (manager == null || assignedSetStatusPanel == null ||
                assignedTranscendenceStatusPanel == null || assignedSetEntryPrefab == null ||
                assignedTranscendenceEntryPrefab == null || assignedTooltip == null)
            {
                Debug.LogWarning(
                    "[RelicEffectStatusUI] GameSceneUIRefs의 현황 Panel, 세트/초월 항목 프리팹 또는 RelicTooltip 참조가 비어 있습니다.");
                return;
            }

            relicManager = manager;
            setStatusPanel = assignedSetStatusPanel;
            transcendenceStatusPanel = assignedTranscendenceStatusPanel;
            setEntryPrefab = assignedSetEntryPrefab;
            transcendenceEntryPrefab = assignedTranscendenceEntryPrefab;
            tooltip = assignedTooltip;

            relicManager.OnRelicStateChanged += Refresh;
            LocalizationText.LanguageChanged += OnTemporaryLanguageChanged;

            if (LocalizationText.IsConfigured)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
                isLocalizationSubscribed = true;
            }
            initialized = true;
            Refresh();
        }

        private void OnDestroy()
        {
            LocalizationText.LanguageChanged -= OnTemporaryLanguageChanged;

            if (isLocalizationSubscribed)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
                isLocalizationSubscribed = false;
            }

            if (relicManager != null)
            {
                relicManager.OnRelicStateChanged -= Refresh;
            }

            RemoveSetEntries();
            RemoveTranscendenceEntries();
        }

        private void OnLocaleChanged(Locale _)
        {
            Refresh();
        }

        private void OnTemporaryLanguageChanged()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (!initialized || relicManager == null) return;

            HashSet<RelicSetBonusData> equippedSets = new HashSet<RelicSetBonusData>(
                relicManager.GetEquippedRelics()
                    .Where(relic => relic != null && relic.synergySetBonusData != null)
                    .Select(relic => relic.synergySetBonusData));

            List<SetStatusDisplayData> activeSetDisplays = equippedSets
                .Where(setBonus => setBonus != null && setBonus.IsAnyTierActive(relicManager))
                .OrderBy(setBonus => setBonus.DisplayName)
                .Select(setBonus => new SetStatusDisplayData(
                    setBonus.statusIcon,
                    $"{setBonus.DisplayName} ({setBonus.GetEquippedCount(relicManager)})",
                    setBonus.DisplayName,
                    setBonus.BuildTooltipText(relicManager),
                    GetSetNameColor(setBonus.GetActiveTierIndex(relicManager))))
                .ToList();

            UpdateSetEntries(activeSetDisplays);

            HashSet<RelicTranscendenceData> transcendenceEffects = new HashSet<RelicTranscendenceData>();
            foreach (RelicSetBonusData setBonus in equippedSets)
            {
                if (setBonus?.linkedTranscendenceEffects == null) continue;

                foreach (RelicTranscendenceData transcendence in setBonus.linkedTranscendenceEffects)
                {
                    if (transcendence != null)
                    {
                        transcendenceEffects.Add(transcendence);
                    }
                }
            }

            List<TranscendenceStatusDisplayData> transcendenceDisplays = transcendenceEffects
                .Where(effect => effect.HasVisibleProgress(relicManager))
                .OrderBy(effect => effect.DisplayName)
                .Select(BuildTranscendenceDisplayData)
                .ToList();

            UpdateTranscendenceEntries(transcendenceDisplays);
            RebuildStatusLayouts();
        }

        private void RebuildStatusLayouts()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(setStatusPanel);
            LayoutRebuilder.ForceRebuildLayoutImmediate(transcendenceStatusPanel);

            foreach (RelicTranscendenceEffectStatusEntryUI entry in transcendenceEntries)
            {
                entry.RebuildLayout();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(transcendenceStatusPanel);
        }

        private static Color GetSetNameColor(int tierIndex)
        {
            return tierIndex < 0
                ? new Color32(128, 128, 128, 255)
                : SetTierNameColors[Mathf.Min(tierIndex, SetTierNameColors.Length - 1)];
        }

        private TranscendenceStatusDisplayData BuildTranscendenceDisplayData(
            RelicTranscendenceData transcendence)
        {
            return new TranscendenceStatusDisplayData(
                transcendence.statusIcon,
                transcendence.DisplayName,
                transcendence.DisplayName,
                transcendence.BuildTooltipText(relicManager, true),
                transcendence.nameColor,
                transcendence.requirements?.Select(requirement => requirement?.setBonusData?.statusIcon).ToArray());
        }

        private void UpdateSetEntries(IReadOnlyList<SetStatusDisplayData> displayData)
        {
            while (setEntries.Count < displayData.Count)
            {
                GameObject entryObject = Instantiate(setEntryPrefab, setStatusPanel, false);
                entryObject.name = "SetEffectStatusEntry";
                entryObject.SetActive(true);

                RelicSetEffectStatusEntryUI entry =
                    entryObject.GetComponent<RelicSetEffectStatusEntryUI>();
                if (entry == null)
                {
                    entry = entryObject.AddComponent<RelicSetEffectStatusEntryUI>();
                }

                if (!entry.Initialize(tooltip))
                {
                    Destroy(entryObject);
                    Debug.LogError(
                        "[RelicEffectStatusUI] 세트 현황 프리팹에서 Image 또는 TextMeshProUGUI를 찾을 수 없습니다.");
                    return;
                }

                setEntries.Add(entry);
            }

            while (setEntries.Count > displayData.Count)
            {
                int lastIndex = setEntries.Count - 1;
                RelicSetEffectStatusEntryUI entry = setEntries[lastIndex];
                setEntries.RemoveAt(lastIndex);
                DestroyEntry(entry);
            }

            for (int i = 0; i < displayData.Count; i++)
            {
                SetStatusDisplayData data = displayData[i];
                setEntries[i].Bind(data.Icon, data.Label, data.TooltipTitle, data.TooltipBody, data.NameColor);
            }
        }

        private void UpdateTranscendenceEntries(
            IReadOnlyList<TranscendenceStatusDisplayData> displayData)
        {
            while (transcendenceEntries.Count < displayData.Count)
            {
                GameObject entryObject = Instantiate(
                    transcendenceEntryPrefab,
                    transcendenceStatusPanel,
                    false);
                entryObject.name = "TranscendenceEffectStatusEntry";
                entryObject.SetActive(true);

                RelicTranscendenceEffectStatusEntryUI entry =
                    entryObject.GetComponent<RelicTranscendenceEffectStatusEntryUI>();
                if (entry == null)
                {
                    entry = entryObject.AddComponent<RelicTranscendenceEffectStatusEntryUI>();
                }

                if (!entry.Initialize(tooltip))
                {
                    Destroy(entryObject);
                    Debug.LogError(
                        "[RelicEffectStatusUI] 초월 현황 프리팹의 Header 구조가 올바르지 않습니다.");
                    return;
                }

                transcendenceEntries.Add(entry);
            }

            while (transcendenceEntries.Count > displayData.Count)
            {
                int lastIndex = transcendenceEntries.Count - 1;
                RelicTranscendenceEffectStatusEntryUI entry = transcendenceEntries[lastIndex];
                transcendenceEntries.RemoveAt(lastIndex);
                DestroyEntry(entry);
            }

            for (int i = 0; i < displayData.Count; i++)
            {
                TranscendenceStatusDisplayData data = displayData[i];
                transcendenceEntries[i].Bind(
                    data.Icon,
                    data.Label,
                    data.TooltipTitle,
                    data.TooltipBody,
                    data.NameColor,
                    data.RequirementIcons);
            }
        }

        private void RemoveSetEntries()
        {
            foreach (RelicSetEffectStatusEntryUI entry in setEntries)
            {
                DestroyEntry(entry);
            }

            setEntries.Clear();
        }

        private void RemoveTranscendenceEntries()
        {
            foreach (RelicTranscendenceEffectStatusEntryUI entry in transcendenceEntries)
            {
                DestroyEntry(entry);
            }

            transcendenceEntries.Clear();
        }

        private static void DestroyEntry(Component entry)
        {
            if (entry == null) return;
            entry.gameObject.SetActive(false);
            Destroy(entry.gameObject);
        }
    }

    public sealed class RelicSetEffectStatusEntryUI : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private Image iconImage;
        private TextMeshProUGUI labelText;
        private RelicTooltip tooltip;
        private string tooltipTitle;
        private string tooltipBody;
        private Color defaultLabelColor;
        private Color tooltipTitleColor;
        private Sprite tooltipIcon;
        private bool hovering;
        private bool initialized;

        public bool Initialize(RelicTooltip assignedTooltip)
        {
            if (initialized) return iconImage != null && labelText != null;

            Transform iconTransform = transform.Find("Image");
            Transform textTransform = transform.Find("Text (TMP)");
            iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            labelText = textTransform != null
                ? textTransform.GetComponent<TextMeshProUGUI>()
                : null;
            tooltip = assignedTooltip;
            initialized = iconImage != null && labelText != null && tooltip != null;
            if (initialized) defaultLabelColor = labelText.color;
            return initialized;
        }

        public void Bind(Sprite icon, string label, string title, string body, Color? nameColor = null)
        {
            if (!initialized) return;

            RelicStatusIconUtility.Apply(iconImage, icon);
            labelText.text = label;
            labelText.color = nameColor ?? defaultLabelColor;
            tooltipTitleColor = labelText.color;
            tooltipIcon = icon;
            tooltipTitle = title;
            tooltipBody = body;
            if (hovering) tooltip.ShowStatus(tooltipTitle, tooltipBody, false, tooltipTitleColor, tooltipIcon);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!initialized) return;
            hovering = true;
            tooltip.ShowStatus(tooltipTitle, tooltipBody, false, tooltipTitleColor, tooltipIcon);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            tooltip?.Hide();
        }

        private void OnDisable()
        {
            hovering = false;
            tooltip?.Hide();
        }
    }

    public sealed class RelicTranscendenceEffectStatusEntryUI : MonoBehaviour
    {
        private Image iconImage;
        private TextMeshProUGUI labelText;
        private RelicStatusTooltipTrigger headerTooltipTrigger;
        private RelicTooltip tooltip;
        private Color defaultLabelColor;
        private bool initialized;

        public bool Initialize(RelicTooltip assignedTooltip)
        {
            if (initialized) return true;

            Transform header = transform.Find("Header");
            Transform iconTransform = header?.Find("Icon");
            Transform textTransform = header?.Find("Text (TMP)");

            iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            labelText = textTransform != null
                ? textTransform.GetComponent<TextMeshProUGUI>()
                : null;
            tooltip = assignedTooltip;

            if (header != null)
            {
                headerTooltipTrigger = header.GetComponent<RelicStatusTooltipTrigger>();
                if (headerTooltipTrigger == null)
                {
                    headerTooltipTrigger = header.gameObject.AddComponent<RelicStatusTooltipTrigger>();
                }
            }

            initialized = iconImage != null && labelText != null &&
                          tooltip != null && headerTooltipTrigger != null &&
                          headerTooltipTrigger.Initialize(tooltip);
            if (initialized) defaultLabelColor = labelText.color;
            return initialized;
        }

        public void Bind(
            Sprite icon,
            string label,
            string title,
            string body,
            Color? nameColor = null,
            IReadOnlyList<Sprite> requirementIcons = null)
        {
            if (!initialized) return;

            RelicStatusIconUtility.Apply(iconImage, icon);
            labelText.text = label;
            labelText.color = nameColor ?? defaultLabelColor;
            headerTooltipTrigger.Bind(title, body, true, labelText.color, icon, requirementIcons);

            RebuildLayout();
        }

        public void RebuildLayout()
        {
            if (!initialized) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        private void OnDisable()
        {
            tooltip?.Hide();
        }
    }

    public sealed class RelicStatusTooltipTrigger : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private RelicTooltip tooltip;
        private string tooltipTitle;
        private string tooltipBody;
        private bool isTranscendence;
        private Color? tooltipTitleColor;
        private Sprite tooltipIcon;
        private IReadOnlyList<Sprite> tooltipRequirementIcons;
        private bool hovering;
        private bool initialized;

        public bool Initialize(RelicTooltip assignedTooltip)
        {
            if (initialized) return tooltip != null;

            tooltip = assignedTooltip;
            initialized = tooltip != null;
            return initialized;
        }

        public void Bind(string title, string body, bool isTranscendence = false,
            Color? titleColor = null, Sprite icon = null, IReadOnlyList<Sprite> requirementIcons = null)
        {
            tooltipTitle = title;
            tooltipBody = body;
            this.isTranscendence = isTranscendence;
            tooltipTitleColor = titleColor;
            tooltipIcon = icon;
            tooltipRequirementIcons = requirementIcons;
            if (hovering) tooltip.ShowStatus(tooltipTitle, tooltipBody, isTranscendence, tooltipTitleColor, tooltipIcon, tooltipRequirementIcons);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!initialized) return;
            hovering = true;
            tooltip.ShowStatus(tooltipTitle, tooltipBody, isTranscendence, tooltipTitleColor, tooltipIcon, tooltipRequirementIcons);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            tooltip?.Hide();
        }

        private void OnDisable()
        {
            hovering = false;
            tooltip?.Hide();
        }
    }

    internal static class RelicStatusIconUtility
    {
        public static void Apply(Image image, Sprite icon)
        {
            if (image == null) return;

            if (icon != null)
            {
                image.sprite = icon;
            }

            image.preserveAspect = true;
            image.enabled = image.sprite != null;
        }
    }
}
