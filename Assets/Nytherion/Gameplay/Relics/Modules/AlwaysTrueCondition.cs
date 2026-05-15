using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using System;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 조건 없이 항상 효과를 발동
    /// </summary>
    [Serializable, RelicDisplayName("항상 발동")]
    public class AlwaysTrueCondition : RelicConditionBase
    {
        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            return true;
        }
    }
}