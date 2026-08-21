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

        public string BuildTooltipText()
        {
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrEmpty(Description))
            {
                builder.AppendLine(Description);
                builder.AppendLine();
            }

            if (bonusModules != null)
            {
                IEnumerable<RelicEffectModule> orderedModules = bonusModules
                    .Where(module => module?.condition is ChainSynergyCondition)
                    .OrderBy(module => ((ChainSynergyCondition)module.condition).requiredChainLength);

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
                    builder.Append('(')
                        .Append(condition.requiredChainLength)
                        .Append(") ")
                        .AppendLine(effectDescription);
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
