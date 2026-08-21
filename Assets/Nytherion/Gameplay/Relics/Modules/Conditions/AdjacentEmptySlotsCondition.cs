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
    /// 상하좌우 인접한 슬롯 중 빈 슬롯의 개수를 세어 일정 개수 이상인지 검사하는 조건 모듈 (길쭉한 가지 전용)
    /// </summary>
    [Serializable, RelicDisplayName("인접한 빈 슬롯 조건")]
    public class AdjacentEmptySlotsCondition : RelicConditionBase
    {
        [Tooltip("요구하는 최소 빈 슬롯 개수")]
        public int requiredEmptySlots = 3;

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            RelicManager relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
            if (relicManager == null) return false;

            foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
            {
                RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                if (block != null && block.SourceData != null)
                {
                    foreach (RelicEffectModule module in block.SourceData.effectModules)
                    {
                        if (module.condition == this)
                        {
                            int r = pair.Value.y;
                            int c = pair.Value.x;

                            int emptyCount = 0;
                            int[] dr = new int[] { -1, 1, 0, 0 };
                            int[] dc = new int[] { 0, 0, -1, 1 };

                            for (int i = 0; i < 4; i++)
                            {
                                int nr = r + dr[i];
                                int nc = c + dc[i];

                                // 그리드 범위 내에 있을 때만 검사
                                if (nr >= 0 && nr < relicManager.GridRows && nc >= 0 && nc < relicManager.GridColumns)
                                {
                                    if (relicManager.GetBlockAt(nr, nc) == null)
                                    {
                                        emptyCount++;
                                    }
                                }
                            }

                            if (emptyCount == 4)
                            {
                                ProgressionManager progressionManager = DataLifetimeScope.Instance != null ? DataLifetimeScope.Instance.GetDataManager<ProgressionManager>() : null;
                                if (progressionManager != null)
                                {
                                    progressionManager.ProcessAction(ProgressionType.SocialDistancingTrigger, 1);
                                }
                            }

                            return emptyCount >= requiredEmptySlots;
                        }
                    }
                }
            }
            return false;
        }
    }
}
