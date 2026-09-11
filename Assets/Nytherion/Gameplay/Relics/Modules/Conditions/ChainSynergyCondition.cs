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
    /// 특정 시너지 시리즈의 연결 체인 길이 또는 보드 장착 개수를 검사하는 조건 모듈
    /// </summary>
    [Serializable, RelicDisplayName("시너지 체인 조건")]
    public class ChainSynergyCondition : RelicConditionBase
    {
        [Tooltip("검사할 시너지 시리즈 ID")]
        public string targetSeriesId;

        [Tooltip("시너지 수치 계산 방식. 기존 데이터는 연결된 최장 체인을 사용합니다.")]
        public ChainSynergyMeasure measure = ChainSynergyMeasure.LongestConnectedChain;

        [Tooltip("요구되는 최소 체인 길이 또는 장착 개수")]
        public int requiredChainLength = 2;

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            if (string.IsNullOrEmpty(targetSeriesId)) return true;

            var relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
            if (relicManager == null) return false;

            int currentValue = measure == ChainSynergyMeasure.EquippedSeriesRelicCount
                ? relicManager.GetEquippedSeriesRelicCount(targetSeriesId)
                : relicManager.GetMaxChainLength(targetSeriesId);

            return currentValue >= requiredChainLength;
        }
    }
}
