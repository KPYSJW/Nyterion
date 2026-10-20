using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nytherion.Core.Managers;
using Nytherion.Gameplay.Relics.Modules;
using Nytherion.Core.Utils;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Relics
{
    /// <summary>
    /// 여러 유물이 공유하는 시너지 세트 보너스 데이터.
    /// 각 단계는 ChainSynergyCondition과 기존 유물 효과를 조합해 설정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRelicSetBonus", menuName = "Data/Relic Set Bonus")]
    public class RelicSetBonusData : ScriptableObject
    {
        private const string ActiveTierColorHex = "#FFFFFF";
        private const string InactiveTierColorHex = "#808080";

        [Header("UI 표시")]
        public string setName_KR;
        public string setName_EN;
        [Tooltip("세트 효과 현황에 표시할 아이콘")]
        public Sprite statusIcon;

        [Header("툴팁 배지")]
        [Tooltip("유물 툴팁의 세트 배지 배경색")]
        public Color badgeBackgroundColor = new Color32(70, 75, 85, 245);
        [Tooltip("유물 툴팁의 세트 배지 글자색")]
        public Color badgeTextColor = Color.white;

        [TextArea] public string description_KR;
        [TextArea] public string description_EN;

        [Tooltip("이 세트가 사용하는 RelicData.synergySeriesId")]
        public string synergySeriesId;

        [Tooltip("조건을 만족한 단계 중 가장 높은 요구치 단계의 모듈만 활성화됩니다.")]
        public List<RelicEffectModule> bonusModules = new List<RelicEffectModule>();

        [Header("초월 효과")]
        [Tooltip("이 세트가 진행도에 기여하는 초월 효과 목록")]
        public List<RelicTranscendenceData> linkedTranscendenceEffects =
            new List<RelicTranscendenceData>();

        public string DisplayName => LocalizationText.Get(
            LocalizationTables.Relics,
            LocalizationKeys.RelicSetName(synergySeriesId),
            setName_KR,
            !string.IsNullOrEmpty(setName_EN) ? setName_EN : name);

        public string Description => LocalizationText.Get(
            LocalizationTables.Relics,
            LocalizationKeys.RelicSetDescription(synergySeriesId),
            description_KR,
            description_EN);

        public int GetEquippedCount(RelicManager relicManager)
        {
            return relicManager != null
                ? relicManager.GetEquippedSeriesRelicCount(synergySeriesId)
                : 0;
        }

        public int GetMinimumActivationCount()
        {
            if (bonusModules == null) return int.MaxValue;

            int minimum = int.MaxValue;
            foreach (RelicEffectModule module in bonusModules)
            {
                if (module?.condition is ChainSynergyCondition condition &&
                    condition.measure == ChainSynergyMeasure.EquippedSeriesRelicCount &&
                    string.Equals(condition.targetSeriesId, synergySeriesId, StringComparison.Ordinal))
                {
                    minimum = Mathf.Min(minimum, condition.requiredChainLength);
                }
            }

            return minimum;
        }

        public bool IsAnyTierActive(RelicManager relicManager)
        {
            int minimum = GetMinimumActivationCount();
            return minimum != int.MaxValue && GetEquippedCount(relicManager) >= minimum;
        }

        /// <summary>요구 개수 순서에 따른 현재 활성 단계. 미활성은 -1, 첫 단계는 0이다.</summary>
        public int GetActiveTierIndex(RelicManager relicManager)
        {
            if (bonusModules == null || relicManager == null) return -1;

            int equippedCount = GetEquippedCount(relicManager);
            return bonusModules
                .Where(module => module?.condition is ChainSynergyCondition condition &&
                    condition.measure == ChainSynergyMeasure.EquippedSeriesRelicCount &&
                    string.Equals(condition.targetSeriesId, synergySeriesId, StringComparison.Ordinal))
                .Select(module => ((ChainSynergyCondition)module.condition).requiredChainLength)
                .Distinct()
                .Count(requiredCount => equippedCount >= requiredCount) - 1;
        }

        public string BuildTooltipText(RelicManager relicManager)
        {
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrEmpty(Description))
            {
                builder.AppendLine(Description);
                builder.AppendLine();
            }

            if (bonusModules != null)
            {
                List<RelicEffectModule> orderedModules = bonusModules
                    .Where(module => module?.condition is ChainSynergyCondition)
                    .OrderBy(module => ((ChainSynergyCondition)module.condition).requiredChainLength)
                    .ToList();

                // 실제 효과 적용과 동일하게 같은 조건 그룹의 가장 높은 충족 단계만 강조한다.
                var activeTiers = orderedModules
                    .Select(module => (ChainSynergyCondition)module.condition)
                    .Where(condition => relicManager != null &&
                        (condition.measure == ChainSynergyMeasure.EquippedSeriesRelicCount
                            ? relicManager.GetEquippedSeriesRelicCount(condition.targetSeriesId)
                            : relicManager.GetMaxChainLength(condition.targetSeriesId)) >= condition.requiredChainLength)
                    .GroupBy(condition => (condition.measure, condition.targetSeriesId))
                    .ToDictionary(group => group.Key, group => group.Max(condition => condition.requiredChainLength));

                foreach (RelicEffectModule module in orderedModules)
                {
                    ChainSynergyCondition condition = (ChainSynergyCondition)module.condition;
                    string effectDescription = !string.IsNullOrEmpty(module.Description)
                        ? module.Description
                        : LocalizationText.Get(
                            LocalizationTables.UI,
                            "ui.relic.effect_description_missing",
                            "효과 설명이 설정되지 않았습니다.",
                            "No effect description has been configured.");
                    string tierColor = activeTiers.TryGetValue(
                        (condition.measure, condition.targetSeriesId), out int activeRequirement) &&
                        condition.requiredChainLength == activeRequirement
                        ? ActiveTierColorHex
                        : InactiveTierColorHex;

                    builder.Append("<color=")
                        .Append(tierColor)
                        .Append(">(")
                        .Append(condition.requiredChainLength)
                        .Append(") ")
                        .Append(effectDescription)
                        .AppendLine("</color>");
                }
            }

            return builder.ToString().TrimEnd();
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(synergySeriesId) || bonusModules == null) return;

            foreach (RelicEffectModule module in bonusModules)
            {
                if (module != null && module.condition is ChainSynergyCondition condition &&
                    string.IsNullOrEmpty(condition.targetSeriesId))
                {
                    condition.targetSeriesId = synergySeriesId;
                }
            }
        }
    }
}
