using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nytherion.Core.Managers;
using Nytherion.Gameplay.Relics.Modules;
using UnityEngine;
using Nytherion.Core.Utils;

namespace Nytherion.Data.ScriptableObjects.Relics
{
    [Serializable]
    public class RelicTranscendenceRequirement
    {
        [Tooltip("초월 효과의 하위 세트")]
        public RelicSetBonusData setBonusData;

        [Min(1)]
        [Tooltip("초월 효과를 완전히 활성화하는 데 필요한 해당 세트 유물 수")]
        public int requiredEquippedCount = 1;
    }

    /// <summary>
    /// 여러 하위 세트의 진행도를 결합해 활성화되는 초월 효과 데이터.
    /// 저장 데이터에는 별도 상태를 기록하지 않고 현재 유물 보드에서 항상 다시 계산한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRelicTranscendence", menuName = "Data/Relic Transcendence")]
    public class RelicTranscendenceData : ScriptableObject
    {
        [Header("UI 표시")]
        public string effectName_KR;
        public string effectName_EN;
        [Tooltip("초월 효과 현황에 표시할 아이콘")]
        public Sprite statusIcon;

        [Header("툴팁 배지")]
        [Tooltip("유물 툴팁의 초월 배지 배경색")]
        public Color badgeBackgroundColor = new Color32(91, 63, 130, 245);
        [Tooltip("유물 툴팁의 초월 배지 글자색")]
        public Color badgeTextColor = new Color32(243, 233, 255, 255);

        [TextArea] public string description_KR;
        [TextArea] public string description_EN;

        [Header("활성 조건")]
        public List<RelicTranscendenceRequirement> requirements =
            new List<RelicTranscendenceRequirement>();

        [Header("활성 효과")]
        [Tooltip("모든 하위 세트 요구치를 만족했을 때 적용할 효과 모듈")]
        public List<RelicEffectModule> effectModules = new List<RelicEffectModule>();

        public string DisplayName => LocalizationText.Get(
            LocalizationTables.Relics,
            LocalizationKeys.RelicTranscendenceName(name),
            effectName_KR,
            !string.IsNullOrEmpty(effectName_EN) ? effectName_EN : name);

        public string Description => LocalizationText.Get(
            LocalizationTables.Relics,
            LocalizationKeys.RelicTranscendenceDescription(name),
            description_KR,
            description_EN);

        public bool HasVisibleProgress(RelicManager relicManager)
        {
            return relicManager != null && requirements != null &&
                   requirements.Any(requirement =>
                       requirement?.setBonusData != null &&
                       requirement.setBonusData.IsAnyTierActive(relicManager));
        }

        public bool IsActive(RelicManager relicManager)
        {
            if (relicManager == null || requirements == null || requirements.Count == 0) return false;

            foreach (RelicTranscendenceRequirement requirement in requirements)
            {
                if (requirement?.setBonusData == null ||
                    !requirement.setBonusData.IsAnyTierActive(relicManager) ||
                    requirement.setBonusData.GetEquippedCount(relicManager) <
                    Mathf.Max(1, requirement.requiredEquippedCount))
                {
                    return false;
                }
            }

            return true;
        }

        public string BuildVisibleProgressText(RelicManager relicManager)
        {
            if (relicManager == null || requirements == null) return string.Empty;

            return string.Join(" , ", requirements
                .Where(requirement => requirement?.setBonusData != null &&
                                      requirement.setBonusData.IsAnyTierActive(relicManager))
                .Select(requirement =>
                    $"[{requirement.setBonusData.DisplayName} ({requirement.setBonusData.GetEquippedCount(relicManager)})]"));
        }

        public string BuildTooltipText(RelicManager relicManager)
        {
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrEmpty(Description))
            {
                builder.AppendLine(Description);
                builder.AppendLine();
            }

            if (requirements != null)
            {
                foreach (RelicTranscendenceRequirement requirement in requirements)
                {
                    if (requirement?.setBonusData == null) continue;

                    int currentCount = requirement.setBonusData.GetEquippedCount(relicManager);
                    int requiredCount = Mathf.Max(1, requirement.requiredEquippedCount);
                    builder.Append(requirement.setBonusData.DisplayName)
                        .Append(": ")
                        .Append(currentCount)
                        .Append('/')
                        .AppendLine(requiredCount.ToString());
                }
            }

            builder.Append(IsActive(relicManager)
                ? LocalizationText.Get(
                    LocalizationTables.UI,
                    "ui.relic.transcendence.active",
                    "\n활성화됨",
                    "\nActive")
                : LocalizationText.Get(
                    LocalizationTables.UI,
                    "ui.relic.transcendence.inactive",
                    "\n활성 조건 미달",
                    "\nActivation requirements not met"));
            return builder.ToString().TrimEnd();
        }

        private void OnValidate()
        {
            if (requirements == null) return;

            foreach (RelicTranscendenceRequirement requirement in requirements)
            {
                if (requirement != null)
                {
                    requirement.requiredEquippedCount = Mathf.Max(1, requirement.requiredEquippedCount);
                }
            }
        }
    }
}
