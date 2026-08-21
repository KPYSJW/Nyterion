using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using System;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 플레이어의 스탯(공격력, 체력, 방어력, 투사체 수 등)을 증감시키는 효과
    /// </summary>
    [Serializable, RelicDisplayName("스탯 변경 효과")]
    public class StatRelicEffect : RelicEffectBase
    {
        [Tooltip("적용할 스탯 변경자들의 목록 (예: 투사체 증가, 공격력 증가 등)")]
        public List<StatModifier> statModifiers = new List<StatModifier>();

        private List<StatModifier> appliedModifiers = new List<StatModifier>();

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null || statModifiers == null) return;

            appliedModifiers.Clear();

            foreach (var modifier in statModifiers)
            {
                float scaledValue = modifier.value + (modifier.valuePerLevel * Mathf.Max(0, level - 1));

                var scaledModifier = new StatModifier
                {
                    stat = modifier.stat,
                    value = scaledValue,
                    valuePerLevel = modifier.valuePerLevel,
                    isPercentage = modifier.isPercentage
                };

                appliedModifiers.Add(scaledModifier);
                playerManager.AddTemporaryStatModifier(scaledModifier);
            }
            
            Debug.Log($"[StatRelicEffect] 스탯 변경 효과가 레벨 {level} 스케일링으로 적용되었습니다.");
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null || appliedModifiers == null) return;

            foreach (var modifier in appliedModifiers)
            {
                playerManager.RemoveTemporaryStatModifier(modifier);
            }
            
            appliedModifiers.Clear();
            Debug.Log($"[StatRelicEffect] 스탯 변경 효과가 해제되었습니다.");
        }
    }
}