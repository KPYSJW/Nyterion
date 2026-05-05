using UnityEngine;
using Nytherion.Core.Managers;
using System;

namespace Nytherion.Gameplay.Engravings.Modules
{
    /// <summary>
    /// 조건 없이 항상 효과를 발동
    /// </summary>
    [Serializable]
    public class AlwaysTrueCondition : EngravingConditionBase
    {
        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            return true;
        }
    }
}