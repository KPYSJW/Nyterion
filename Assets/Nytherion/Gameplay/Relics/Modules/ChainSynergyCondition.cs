using System;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 특정 시너지 시리즈의 체인 길이가 요구치 이상인지 검사하는 조건 모듈
    /// </summary>
    [Serializable, RelicDisplayName("시너지 체인 조건")]
    public class ChainSynergyCondition : RelicConditionBase
    {
        [Tooltip("검사할 시너지 시리즈 ID")]
        public string targetSeriesId;

        [Tooltip("요구되는 최소 체인 길이")]
        public int requiredChainLength = 2;

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            if (string.IsNullOrEmpty(targetSeriesId)) return true;

            var relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
            if (relicManager == null) return false;

            int currentChainLength = relicManager.GetMaxChainLength(targetSeriesId);
            return currentChainLength >= requiredChainLength;
        }
    }
}