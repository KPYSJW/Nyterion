using System;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using System.Collections.Generic;
using Nytherion.GamePlay.Relics;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 시리즈별 첫 유물을 제외한 추가 장착 유물의 합계를 검사한다.
    /// </summary>
    [Serializable, RelicDisplayName("시리즈별 추가 장착 유물 수 조건")]
    public class TotalChainLinksCondition : RelicConditionBase
    {
        [Tooltip("시리즈별 첫 유물을 제외한 추가 장착 유물의 최소 합계")]
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
                int count = relicManager.GetEquippedSeriesRelicCount(seriesId);
                if (count >= 2)
                {
                    totalLinks += (count - 1);
                }
            }

            return totalLinks >= requiredTotalLinks;
        }
    }
}
