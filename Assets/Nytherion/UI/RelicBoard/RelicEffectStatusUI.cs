using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nytherion.UI.RelicBoard
{
    /// <summary>
    /// 세트와 초월 효과를 각각 전용 프리팹으로 표시한다.
    /// 초월 효과의 하위 세트는 초월 프리팹 안의 RequirementEntry를 템플릿으로 복제한다.
    /// </summary>
    public sealed class RelicEffectStatusUI : MonoBehaviour
    {
        private sealed class SetStatusDisplayData
        {
            public Sprite Icon { get; }
            public string Label { get; }
            public string TooltipTitle { get; }
            public string TooltipBody { get; }

            public SetStatusDisplayData(
                Sprite icon,
                string label,
                string tooltipTitle,
                string tooltipBody)
            {
                Icon = icon;
                Label = label;
                TooltipTitle = tooltipTitle;
                TooltipBody = tooltipBody;
            }
        }

        private sealed class RequirementStatusDisplayData
        {
            public Sprite Icon { get; }
            public string Label { get; }
            public string TooltipTitle { get; }
            public string TooltipBody { get; }
            public int EquippedCount { get; }

            public RequirementStatusDisplayData(
                Sprite icon,
                string label,
                string tooltipTitle,
                string tooltipBody,
                int equippedCount)
            {
                Icon = icon;
                Label = label;
                TooltipTitle = tooltipTitle;
                TooltipBody = tooltipBody;
                EquippedCount = equippedCount;
            }
        }

        private sealed class TranscendenceStatusDisplayData
        {
            public Sprite Icon { get; }
            public string Label { get; }
            public string TooltipTitle { get; }
            public string TooltipBody { get; }
            public IReadOnlyList<RequirementStatusDisplayData> Requirements { get; }

            public TranscendenceStatusDisplayData(
                Sprite icon,
                string label,
                string tooltipTitle,
                string tooltipBody,
                IReadOnlyList<RequirementStatusDisplayData> requirements)
            {
                Icon = icon;
                Label = label;
                TooltipTitle = tooltipTitle;
                TooltipBody = tooltipBody;
                Requirements = requirements;
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
            initialized = true;
            Refresh();
        }

        private void OnDestroy()
        {
            if (relicManager != null)
            {
                relicManager.OnRelicStateChanged -= Refresh;
            }

            RemoveSetEntries();
            RemoveTranscendenceEntries();
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
                    setBonus.BuildTooltipText()))
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

        private TranscendenceStatusDisplayData BuildTranscendenceDisplayData(
            RelicTranscendenceData transcendence)
        {
            IEnumerable<RelicTranscendenceRequirement> requirementSource =
                transcendence.requirements ?? Enumerable.Empty<RelicTranscendenceRequirement>();

            List<RequirementStatusDisplayData> requirements = requirementSource
                .Where(requirement => requirement?.setBonusData != null &&
                                      requirement.setBonusData.IsAnyTierActive(relicManager))
                .Select(requirement =>
                {
                    RelicSetBonusData setBonus = requirement.setBonusData;
                    int equippedCount = setBonus.GetEquippedCount(relicManager);
                    return new RequirementStatusDisplayData(
                        setBonus.statusIcon,
                        $"{setBonus.DisplayName} ({equippedCount})",
                        setBonus.DisplayName,
                        setBonus.BuildTooltipText(),
                        equippedCount);
                })
                .ToList();

            int totalEquippedCount = requirements
                .Sum(requirement => requirement.EquippedCount);

            return new TranscendenceStatusDisplayData(
                transcendence.statusIcon,
                $"{transcendence.DisplayName} ({totalEquippedCount})",
                transcendence.DisplayName,
                transcendence.BuildTooltipText(relicManager),
                requirements);
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
                setEntries[i].Bind(data.Icon, data.Label, data.TooltipTitle, data.TooltipBody);
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
                        "[RelicEffectStatusUI] 초월 현황 프리팹의 Header 또는 RequirementContainer 구조가 올바르지 않습니다.");
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
                List<RelicRequirementStatusData> requirements = data.Requirements
                    .Select(requirement => new RelicRequirementStatusData(
                        requirement.Icon,
                        requirement.Label,
                        requirement.TooltipTitle,
                        requirement.TooltipBody))
                    .ToList();

                transcendenceEntries[i].Bind(
                    data.Icon,
                    data.Label,
                    data.TooltipTitle,
                    data.TooltipBody,
                    requirements);
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

    public sealed class RelicRequirementStatusData
    {
        public Sprite Icon { get; }
        public string Label { get; }
        public string TooltipTitle { get; }
        public string TooltipBody { get; }

        public RelicRequirementStatusData(
            Sprite icon,
            string label,
            string tooltipTitle,
            string tooltipBody)
        {
            Icon = icon;
            Label = label;
            TooltipTitle = tooltipTitle;
            TooltipBody = tooltipBody;
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
            return initialized;
        }

        public void Bind(Sprite icon, string label, string title, string body)
        {
            if (!initialized) return;

            RelicStatusIconUtility.Apply(iconImage, icon);
            labelText.text = label;
            tooltipTitle = title;
            tooltipBody = body;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (initialized) tooltip.ShowStatus(tooltipTitle, tooltipBody);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltip?.Hide();
        }

        private void OnDisable()
        {
            tooltip?.Hide();
        }
    }

    public sealed class RelicTranscendenceEffectStatusEntryUI : MonoBehaviour
    {
        private readonly List<RelicRequirementStatusEntryUI> requirementEntries =
            new List<RelicRequirementStatusEntryUI>();

        private Image iconImage;
        private TextMeshProUGUI labelText;
        private RectTransform requirementContainer;
        private GameObject requirementTemplate;
        private RelicStatusTooltipTrigger headerTooltipTrigger;
        private RelicTooltip tooltip;
        private bool initialized;

        public bool Initialize(RelicTooltip assignedTooltip)
        {
            if (initialized) return true;

            Transform header = transform.Find("Header");
            Transform iconTransform = header?.Find("Icon");
            Transform textTransform = header?.Find("Text (TMP)");
            Transform containerTransform = transform.Find("RequirementContainer");
            Transform templateTransform = containerTransform?.Find("RequirementEntry");

            iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            labelText = textTransform != null
                ? textTransform.GetComponent<TextMeshProUGUI>()
                : null;
            requirementContainer = containerTransform as RectTransform;
            requirementTemplate = templateTransform != null ? templateTransform.gameObject : null;
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
                          requirementContainer != null && requirementTemplate != null &&
                          tooltip != null && headerTooltipTrigger != null &&
                          headerTooltipTrigger.Initialize(tooltip);
            if (initialized)
            {
                requirementTemplate.SetActive(false);
            }

            return initialized;
        }

        public void Bind(
            Sprite icon,
            string label,
            string title,
            string body,
            IReadOnlyList<RelicRequirementStatusData> requirements)
        {
            if (!initialized) return;

            RelicStatusIconUtility.Apply(iconImage, icon);
            labelText.text = label;
            headerTooltipTrigger.Bind(title, body);

            UpdateRequirements(requirements);
            RebuildLayout();
        }

        public void RebuildLayout()
        {
            if (!initialized) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(requirementContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        private void UpdateRequirements(IReadOnlyList<RelicRequirementStatusData> requirements)
        {
            while (requirementEntries.Count < requirements.Count)
            {
                GameObject requirementObject = Instantiate(
                    requirementTemplate,
                    requirementContainer,
                    false);
                requirementObject.name = "RequirementEntry";
                requirementObject.SetActive(true);

                RelicRequirementStatusEntryUI requirementEntry =
                    requirementObject.GetComponent<RelicRequirementStatusEntryUI>();
                if (requirementEntry == null)
                {
                    requirementEntry = requirementObject.AddComponent<RelicRequirementStatusEntryUI>();
                }

                if (!requirementEntry.Initialize(tooltip))
                {
                    Destroy(requirementObject);
                    Debug.LogError(
                        "[RelicTranscendenceEffectStatusEntryUI] RequirementEntry의 Icon 또는 Label을 찾을 수 없습니다.");
                    return;
                }

                requirementEntries.Add(requirementEntry);
            }

            while (requirementEntries.Count > requirements.Count)
            {
                int lastIndex = requirementEntries.Count - 1;
                RelicRequirementStatusEntryUI entry = requirementEntries[lastIndex];
                requirementEntries.RemoveAt(lastIndex);
                if (entry != null)
                {
                    entry.gameObject.SetActive(false);
                    Destroy(entry.gameObject);
                }
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                RelicRequirementStatusData requirement = requirements[i];
                requirementEntries[i].Bind(
                    requirement.Icon,
                    requirement.Label,
                    requirement.TooltipTitle,
                    requirement.TooltipBody);
            }
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
        private bool initialized;

        public bool Initialize(RelicTooltip assignedTooltip)
        {
            if (initialized) return tooltip != null;

            tooltip = assignedTooltip;
            initialized = tooltip != null;
            return initialized;
        }

        public void Bind(string title, string body)
        {
            tooltipTitle = title;
            tooltipBody = body;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (initialized) tooltip.ShowStatus(tooltipTitle, tooltipBody);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltip?.Hide();
        }

        private void OnDisable()
        {
            tooltip?.Hide();
        }
    }

    public sealed class RelicRequirementStatusEntryUI : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private Image iconImage;
        private TextMeshProUGUI labelText;
        private RelicTooltip tooltip;
        private string tooltipTitle;
        private string tooltipBody;
        private bool initialized;

        public bool Initialize(RelicTooltip assignedTooltip)
        {
            if (initialized) return true;

            Transform iconTransform = transform.Find("Icon");
            Transform labelTransform = transform.Find("Label");
            iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            labelText = labelTransform != null
                ? labelTransform.GetComponent<TextMeshProUGUI>()
                : null;
            tooltip = assignedTooltip;
            initialized = iconImage != null && labelText != null && tooltip != null;
            return initialized;
        }

        public void Bind(Sprite icon, string label, string title, string body)
        {
            if (!initialized) return;

            RelicStatusIconUtility.Apply(iconImage, icon);
            labelText.text = label;
            tooltipTitle = title;
            tooltipBody = body;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (initialized) tooltip.ShowStatus(tooltipTitle, tooltipBody);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltip?.Hide();
        }

        private void OnDisable()
        {
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
