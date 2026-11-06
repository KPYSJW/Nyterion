using System;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    public enum ChainSynergyMeasure
    {
        LongestConnectedChain,
        EquippedSeriesRelicCount
    }

    /// <summary>
    /// 특정 시너지 시리즈의 보드 장착 개수를 검사하는 조건 모듈
    /// </summary>
    [Serializable, RelicDisplayName("시너지 장착 개수 조건")]
    public class ChainSynergyCondition : RelicConditionBase
    {
        [Tooltip("검사할 시너지 시리즈 ID")]
        public string targetSeriesId;

        // 기존 에셋의 직렬화 필드를 보존하며, 모든 조건은 장착 개수로 판정한다.
        [HideInInspector]
        public ChainSynergyMeasure measure = ChainSynergyMeasure.EquippedSeriesRelicCount;

        [Tooltip("요구되는 최소 장착 개수")]
        public int requiredChainLength = 2;

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            if (string.IsNullOrEmpty(targetSeriesId)) return true;

            var relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
            if (relicManager == null) return false;

            int currentValue = relicManager.GetEquippedSeriesRelicCount(targetSeriesId);

            return currentValue >= requiredChainLength;
        }
    }
}
