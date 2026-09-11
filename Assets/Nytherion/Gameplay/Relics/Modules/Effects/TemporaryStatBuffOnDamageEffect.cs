using System;
using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 플레이어가 피해를 받으면 일정 시간 동안 스탯 보너스를 적용한다.
    /// 지속 시간 중 다시 피해를 받으면 남은 시간을 갱신한다.
    /// </summary>
    [Serializable, RelicDisplayName("피격 시 임시 스탯 강화")]
    public class TemporaryStatBuffOnDamageEffect : RelicEffectBase
    {
        [Min(0.1f)]
        [Tooltip("피격 후 강화가 유지되는 시간")]
        public float duration = 4f;

        [Tooltip("피격 시 임시로 적용할 스탯 변경")]
        public List<StatModifier> statModifiers = new List<StatModifier>();

        [NonSerialized] private PlayerManager cachedPlayerManager;
        [NonSerialized] private Coroutine expirationCoroutine;
        [NonSerialized] private List<StatModifier> appliedModifiers;
        [NonSerialized] private int currentLevel;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(cachedPlayerManager, currentLevel);
            if (playerManager == null) return;

            cachedPlayerManager = playerManager;
            currentLevel = level;
            PlayerHealth.OnPlayerDamaged -= HandlePlayerDamaged;
            PlayerHealth.OnPlayerDamaged += HandlePlayerDamaged;
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            PlayerHealth.OnPlayerDamaged -= HandlePlayerDamaged;

            if (cachedPlayerManager != null && expirationCoroutine != null)
            {
                cachedPlayerManager.StopCoroutine(expirationCoroutine);
                expirationCoroutine = null;
            }

            RemoveAppliedModifiers();
            cachedPlayerManager = null;
        }

        private void HandlePlayerDamaged(float _)
        {
            if (cachedPlayerManager == null) return;

            if (appliedModifiers == null || appliedModifiers.Count == 0)
            {
                appliedModifiers = CreateScaledModifiers(currentLevel);
                foreach (StatModifier modifier in appliedModifiers)
                {
                    cachedPlayerManager.AddTemporaryStatModifier(modifier);
                }
            }

            if (expirationCoroutine != null)
            {
                cachedPlayerManager.StopCoroutine(expirationCoroutine);
            }
            expirationCoroutine = cachedPlayerManager.StartCoroutine(RemoveAfterDuration());
        }

        private IEnumerator RemoveAfterDuration()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, duration));
            expirationCoroutine = null;
            RemoveAppliedModifiers();
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
