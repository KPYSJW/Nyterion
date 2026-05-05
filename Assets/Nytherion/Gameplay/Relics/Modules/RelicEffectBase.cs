using UnityEngine;
using Nytherion.Core.Managers;
using System;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable]
    public abstract class RelicEffectBase
    {
        /// <summary>
        /// 효과를 플레이어에게 적용
        /// </summary>
        public abstract void ApplyEffect(PlayerManager playerManager, int level);

        /// <summary>
        /// 효과를 플레이어에게서 제거
        /// </summary>
        public abstract void RemoveEffect(PlayerManager playerManager, int level);
    }
}