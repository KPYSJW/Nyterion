using System;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using System.Collections.Generic;
using Nytherion.GamePlay.Relics;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 각인 보드의 정중앙 슬롯에 배치되었는지 검사하는 조건 모듈 
    /// </summary>
    [Serializable, RelicDisplayName("정중앙 배치 조건")]
    public class CenterPebbleCondition : RelicConditionBase
    {
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

                            int centerRow = relicManager.GridRows / 2;
                            int centerCol = relicManager.GridColumns / 2;

                            if (r == centerRow && c == centerCol)
                            {
                                return true;
                            }
                            return false;
                        }
                    }
                }
            }
            return false;
        }
    }
}
