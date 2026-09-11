using System;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 현재 체력이 기준 이하일 때만 스탯 보너스를 유지한다.
    /// </summary>
    [Serializable, RelicDisplayName("낮은 체력 스탯 강화")]
    public class HealthThresholdStatEffect : RelicEffectBase
    {
        [Range(1f, 100f)]
        [Tooltip("효과가 활성화되는 최대 체력 비율")]
        public float thresholdPercent = 40f;

        [Tooltip("체력 조건을 만족하는 동안 적용할 스탯 변경")]
        public List<StatModifier> statModifiers = new List<StatModifier>();

        [NonSerialized] private PlayerManager cachedPlayerManager;
        [NonSerialized] private List<StatModifier> appliedModifiers;
        [NonSerialized] private int currentLevel;
        [NonSerialized] private bool isUpdating;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(cachedPlayerManager, currentLevel);
            if (playerManager == null) return;

            cachedPlayerManager = playerManager;
            currentLevel = level;
            PlayerHealth.OnHealthChanged -= HandleHealthChanged;
            PlayerHealth.OnHealthChanged += HandleHealthChanged;

            PlayerHealth health = cachedPlayerManager.playerHealth;
            if (health != null)
            {
                HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            PlayerHealth.OnHealthChanged -= HandleHealthChanged;
            RemoveAppliedModifiers();
            cachedPlayerManager = null;
        }

        private void HandleHealthChanged(float currentHealth, float maxHealth)
        {
            if (cachedPlayerManager == null || isUpdating) return;

            bool shouldBeActive = maxHealth > 0f &&
                                  currentHealth / maxHealth * 100f <= thresholdPercent;
            bool isActive = appliedModifiers != null && appliedModifiers.Count > 0;

            if (shouldBeActive == isActive) return;

            isUpdating = true;
            if (shouldBeActive)
            {
                appliedModifiers = CreateScaledModifiers(currentLevel);
                foreach (StatModifier modifier in appliedModifiers)
                {
                    cachedPlayerManager.AddTemporaryStatModifier(modifier);
                }
            }
            else
            {
                RemoveAppliedModifiers();
            }
            isUpdating = false;
        }

        private List<StatModifier> CreateScaledModifiers(int level)
        {
            List<StatModifier> result = new List<StatModifier>();
            if (statModifiers == null) return result;

            foreach (StatModifier modifier in statModifiers)
            {
                if (modifier == null) continue;
                result.Add(new StatModifier
                {
                    stat = modifier.stat,
                    value = modifier.value + modifier.valuePerLevel * Mathf.Max(0, level - 1),
                    valuePerLevel = 0f,
                    isPercentage = modifier.isPercentage
                });
            }
            return result;
        }

        private void RemoveAppliedModifiers()
        {
            if (cachedPlayerManager == null || appliedModifiers == null) return;

            foreach (StatModifier modifier in appliedModifiers)
            {
                cachedPlayerManager.RemoveTemporaryStatModifier(modifier);
            }
            appliedModifiers.Clear();
        }
    }
}
