using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 플레이어의 치명타가 적중하면 주변 적에게 연쇄 번개 피해를 전달한다.
    /// 연쇄 피해는 다시 이 효과를 발동시키지 않는다.
    /// </summary>
    [Serializable, RelicDisplayName("치명타 연쇄 번개")]
    public class CriticalChainLightningEffect : RelicEffectBase
    {
        [Range(0f, 2f)]
        [Tooltip("원래 치명타 피해 대비 연쇄 피해 배율")]
        public float damageRatio = 0.3f;

        [Min(0.1f)]
        [Tooltip("연쇄 대상을 탐색하는 반경")]
        public float searchRadius = 5f;

        [Min(1)]
        [Tooltip("한 번에 피해를 전달할 최대 적 수")]
        public int maxTargets = 3;

        [Min(0.1f)]
        [Tooltip("연쇄 대상에게 표시할 감전 효과 지속 시간")]
        public float lightningDuration = 3f;

        [NonSerialized] private EventManager cachedEventManager;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            cachedEventManager = playerManager != null ? playerManager.EventManager : null;
            if (cachedEventManager == null) return;

            cachedEventManager.OnEnemyDamagedByPlayerDetailed -= HandlePlayerDamage;
            cachedEventManager.OnEnemyDamagedByPlayerDetailed += HandlePlayerDamage;
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (cachedEventManager != null)
            {
                cachedEventManager.OnEnemyDamagedByPlayerDetailed -= HandlePlayerDamage;
            }
            cachedEventManager = null;
        }

        private void HandlePlayerDamage(PlayerDamageEventData eventData)
        {
            if (!eventData.IsCritical || eventData.IsChainDamage || eventData.Target == null) return;

            Collider2D[] colliders = Physics2D.OverlapCircleAll(
                eventData.Target.transform.position,
                Mathf.Max(0.1f, searchRadius));
            HashSet<EnemyBase> damagedEnemies = new HashSet<EnemyBase>();
            int damagedCount = 0;

            foreach (Collider2D hit in colliders)
            {
                EnemyBase enemy = hit != null ? hit.GetComponentInParent<EnemyBase>() : null;
                if (enemy == null || enemy == eventData.Target || enemy.isDead ||
                    !damagedEnemies.Add(enemy))
                {
                    continue;
                }

                enemy.TakeDamage(eventData.DamageAmount * Mathf.Max(0f, damageRatio), true);
                if (!enemy.isDead && enemy.TryGetComponent(out StatusEffectManager statusEffectManager))
                {
                    statusEffectManager.ApplyEffect(new LightningEffect(
                        Mathf.Max(0.1f, lightningDuration)));
                }

                damagedCount++;
                if (damagedCount >= Mathf.Max(1, maxTargets)) break;
            }
        }
    }
}
