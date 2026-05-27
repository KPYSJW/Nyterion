using Nytherion.Core.Managers;
using System;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable]
    public abstract class RelicConditionBase
    {
        /// <summary>
        /// 해당 효과가 발동되기 위한 조건을 검사
        /// </summary>
        public abstract bool IsConditionMet(PlayerManager playerManager, int level);
    }
}