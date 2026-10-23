using System;
using System.Collections;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 살아 있는 플레이어의 체력이 기준 이하일 때 매초 최대 체력에 비례해 재생한다.
    /// </summary>
    [Serializable, RelicDisplayName("낮은 체력에서 초당 체력 재생")]
    public sealed class HealthThresholdRegenerationEffect : RelicEffectBase
    {
        [Range(1f, 100f)]
        [Tooltip("체력 재생이 활성화되는 최대 체력 비율")]
        public float thresholdPercent = 40f;

        [Range(0f, 1f)]
        [Tooltip("매초 재생할 최대 체력의 비율 (0.03 = 3%)")]
        public float maxHealthRatioPerSecond = 0.03f;

        [NonSerialized] private PlayerManager cachedPlayerManager;
        [NonSerialized] private Coroutine regenerationCoroutine;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(cachedPlayerManager, level);
            if (playerManager == null) return;

            cachedPlayerManager = playerManager;
            regenerationCoroutine = cachedPlayerManager.StartCoroutine(RegenerateHealth());
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (cachedPlayerManager != null && regenerationCoroutine != null)
            {
                cachedPlayerManager.StopCoroutine(regenerationCoroutine);
            }
            regenerationCoroutine = null;
            cachedPlayerManager = null;
        }

        private IEnumerator RegenerateHealth()
        {
            WaitForSeconds interval = new WaitForSeconds(1f);
            while (cachedPlayerManager != null)
            {
                yield return interval;

                PlayerHealth health = cachedPlayerManager != null ? cachedPlayerManager.playerHealth : null;
                if (health == null || health.CurrentHealth <= 0f || health.MaxHealth <= 0f ||
                    health.CurrentHealth / health.MaxHealth * 100f > thresholdPercent)
                {
                    continue;
                }

                float amount = health.MaxHealth * Mathf.Clamp01(maxHealthRatioPerSecond);
                if (amount > 0f)
                {
                    health.Heal(amount);
                }
            }
        }
    }
}
