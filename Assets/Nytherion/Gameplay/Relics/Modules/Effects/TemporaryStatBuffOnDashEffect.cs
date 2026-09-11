using System;
using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 대시가 시작되면 일정 시간 동안 스탯 보너스를 적용한다.
    /// </summary>
    [Serializable, RelicDisplayName("대시 후 임시 스탯 강화")]
    public class TemporaryStatBuffOnDashEffect : RelicEffectBase
    {
        [Min(0.1f)] public float duration = 3f;
        public List<StatModifier> statModifiers = new List<StatModifier>();

        [NonSerialized] private PlayerManager cachedPlayerManager;
        [NonSerialized] private EventManager cachedEventManager;
        [NonSerialized] private List<StatModifier> appliedModifiers;
        [NonSerialized] private Coroutine expirationCoroutine;
        [NonSerialized] private int currentLevel;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(cachedPlayerManager, currentLevel);
            if (playerManager == null) return;

            cachedPlayerManager = playerManager;
            cachedEventManager = playerManager.EventManager;
            currentLevel = level;
            appliedModifiers = new List<StatModifier>();

            if (cachedEventManager == null) return;
            cachedEventManager.OnPlayerDashStarted -= HandlePlayerDashStarted;
            cachedEventManager.OnPlayerDashStarted += HandlePlayerDashStarted;
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (cachedEventManager != null)
            {
                cachedEventManager.OnPlayerDashStarted -= HandlePlayerDashStarted;
            }

            if (cachedPlayerManager != null && expirationCoroutine != null)
            {
                cachedPlayerManager.StopCoroutine(expirationCoroutine);
                expirationCoroutine = null;
            }

            RemoveAppliedModifiers();
            cachedEventManager = null;
            cachedPlayerManager = null;
        }

        private void HandlePlayerDashStarted()
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
