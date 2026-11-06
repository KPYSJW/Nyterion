using System;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using System.Collections.Generic;
using Nytherion.GamePlay.Relics;
using Nytherion.Core.Systems;
using Nytherion.Core.Enums;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 같은 시리즈 유물을 2개 이상 장착한 시리즈의 수가 짝수인지 검사한다.
    /// </summary>
    [Serializable, RelicDisplayName("활성 시너지 세트 개수 짝수 조건")]
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
                int count = relicManager.GetEquippedSeriesRelicCount(seriesId);
                if (count >= 2)
                {
                    activeChainCount++;
                }
            }

            bool isMet = activeChainCount > 0 && activeChainCount % 2 == 0;
            if (isMet)
            {
                ProgressionManager progressionManager = DataLifetimeScope.Instance != null ? DataLifetimeScope.Instance.GetDataManager<ProgressionManager>() : null;
                if (progressionManager != null)
                {
                    progressionManager.ProcessAction(ProgressionType.SqueakyGearTrigger, 1);
                }
            }

            return isMet;
        }
    }
}
