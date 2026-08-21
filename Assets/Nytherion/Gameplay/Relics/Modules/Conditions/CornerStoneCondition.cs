using System;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using System.Collections.Generic;
using Nytherion.GamePlay.Relics;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 각인 보드의 가장자리 슬롯에 배치되었는지 검사하는 조건 모듈 
    /// </summary>
    [Serializable, RelicDisplayName("구석 배치 조건")]
    public class CornerStoneCondition : RelicConditionBase
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
                            
                            // 보드의 가장자리인지 체크
                            if (r == 0 || r == relicManager.GridRows - 1 || c == 0 || c == relicManager.GridColumns - 1)
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
