using System;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using System.Collections.Generic;
using Nytherion.GamePlay.Relics;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 활성화된 전체 시너지 체인의 총 링크 수(연결고리 수)가 일정 개수 이상인지 검사하는 조건 모듈 (꼬인 실타래 전용)
    /// </summary>
    [Serializable, RelicDisplayName("총 체인 링크 수 조건")]
    public class TotalChainLinksCondition : RelicConditionBase
    {
        [Tooltip("요구되는 최소 활성화된 체인 링크 수")]
        public int requiredTotalLinks = 3;

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            RelicManager relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
            if (relicManager == null) return false;

            HashSet<string> seriesIds = new HashSet<string>();
            foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
            {
                RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                if (block != null && block.SourceData != null && !string.IsNullOrEmpty(block.SourceData.synergySeriesId))
                {
                    seriesIds.Add(block.SourceData.synergySeriesId);
                }
            }

            int totalLinks = 0;
            foreach (string seriesId in seriesIds)
            {
                int length = relicManager.GetMaxChainLength(seriesId);
                if (length >= 2)
                {
                    totalLinks += (length - 1);
                }
            }

            return totalLinks >= requiredTotalLinks;
        }
    }
}
