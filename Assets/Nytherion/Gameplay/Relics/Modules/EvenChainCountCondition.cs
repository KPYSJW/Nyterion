using System;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using System.Collections.Generic;
using Nytherion.GamePlay.Relics;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 활성화된 시너지 체인의 총 개수가 짝수(0개 초과)인지 검사하는 조건 모듈 (삐걱이는 톱니 전용)
    /// </summary>
    [Serializable, RelicDisplayName("활성화 체인 개수 짝수 조건")]
    public class EvenChainCountCondition : RelicConditionBase
    {
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

            int activeChainCount = 0;
            foreach (string seriesId in seriesIds)
            {
                int length = relicManager.GetMaxChainLength(seriesId);
                if (length >= 2)
                {
                    activeChainCount++;
                }
            }

            return activeChainCount > 0 && activeChainCount % 2 == 0;
        }
    }
}
