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
    /// 독 피해와 지속 시간을 강화하고, 중독된 적 처치 시 전염과 폭발을 발생시킨다.
    /// </summary>
    [Serializable, RelicDisplayName("역병 세트 강화")]
    public class PoisonSetBonusEffect : RelicEffectBase
    {
        [Min(0f)] public float durationMultiplier = 1f;
        [Min(0f)] public float tickDamageMultiplier = 1f;

        [Header("독 전염")]
        public bool spreadOnPoisonedEnemyDeath;
        [Min(0.1f)] public float spreadRadius = 5f;
        [Min(1)] public int maxSpreadTargets = 3;
        [Min(0f)] public float spreadDamageMultiplier = 0.75f;
        [Min(0.1f)] public float spreadDuration = 5f;
        [Min(1)] public int maxSpreadStacks = 2;

        [Header("독 폭발")]
        public bool explodeOnPoisonedEnemyDeath;
        [Min(0.1f)] public float explosionRadius = 4f;
        [Min(0f)] public float explosionDamageMultiplier = 2f;

        [NonSerialized] private PoisonSetBonusRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<PoisonSetBonusRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<PoisonSetBonusRuntime>();
            }

            appliedRuntime.SetBonus(this, playerManager, new PoisonSetBonusValues
            {
                DurationMultiplier = durationMultiplier,
                TickDamageMultiplier = tickDamageMultiplier,
                SpreadOnDeath = spreadOnPoisonedEnemyDeath,
                SpreadRadius = spreadRadius,
                MaxSpreadTargets = maxSpreadTargets,
                SpreadDamageMultiplier = spreadDamageMultiplier,
                SpreadDuration = spreadDuration,
                MaxSpreadStacks = maxSpreadStacks,
                ExplodeOnDeath = explodeOnPoisonedEnemyDeath,
                ExplosionRadius = explosionRadius,
                ExplosionDamageMultiplier = explosionDamageMultiplier
            });
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<PoisonSetBonusRuntime>();
            }

            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public struct PoisonSetBonusValues
    {
        public float DurationMultiplier;
        public float TickDamageMultiplier;
        public bool SpreadOnDeath;
        public float SpreadRadius;
        public int MaxSpreadTargets;
        public float SpreadDamageMultiplier;
        public float SpreadDuration;
        public int MaxSpreadStacks;
        public bool ExplodeOnDeath;
        public float ExplosionRadius;
        public float ExplosionDamageMultiplier;
    }

    /// <summary>
    /// 활성화된 역병 세트 보정을 합산하고 적 처치 이벤트를 처리한다.
    /// </summary>
    public sealed class PoisonSetBonusRuntime : MonoBehaviour
    {
        private readonly Dictionary<PoisonSetBonusEffect, PoisonSetBonusValues> bonuses =
            new Dictionary<PoisonSetBonusEffect, PoisonSetBonusValues>();

        private PlayerManager playerManager;
        private EventManager eventManager;
        private bool isResolvingExplosion;

        public float DurationMultiplier { get; private set; } = 1f;
        public float TickDamageMultiplier { get; private set; } = 1f;
        private bool SpreadOnDeath { get; set; }
        private float SpreadRadius { get; set; }
        private int MaxSpreadTargets { get; set; }
        private float SpreadDamageMultiplier { get; set; }
        private float SpreadDuration { get; set; }
        private int MaxSpreadStacks { get; set; }
        private bool ExplodeOnDeath { get; set; }
        private float ExplosionRadius { get; set; }
        private float ExplosionDamageMultiplier { get; set; }

        public void SetBonus(
            PoisonSetBonusEffect source,
            PlayerManager currentPlayerManager,
            PoisonSetBonusValues values)
        {
            if (source == null || currentPlayerManager == null) return;

            bonuses[source] = values;
            ConfigurePlayer(currentPlayerManager);
            Recalculate();
        }

        public void RemoveBonus(PoisonSetBonusEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        public void ApplyTo(PoisonEffect poisonEffect)
        {
            if (poisonEffect == null) return;
            poisonEffect.ApplySetBonus(DurationMultiplier, TickDamageMultiplier);
        }

        private void ConfigurePlayer(PlayerManager currentPlayerManager)
        {
            playerManager = currentPlayerManager;
            EventManager newEventManager = currentPlayerManager.EventManager;
            if (eventManager == newEventManager) return;

            if (eventManager != null)
            {
                eventManager.OnEnemyDied -= HandleEnemyDied;
            }

            eventManager = newEventManager;
            if (eventManager != null)
            {
                eventManager.OnEnemyDied += HandleEnemyDied;
            }
        }

        private void Recalculate()
        {
            DurationMultiplier = 1f;
            TickDamageMultiplier = 1f;
            SpreadOnDeath = false;
            SpreadRadius = 0f;
            MaxSpreadTargets = 0;
            SpreadDamageMultiplier = 0f;
            SpreadDuration = 0f;
            MaxSpreadStacks = 1;
            ExplodeOnDeath = false;
            ExplosionRadius = 0f;
            ExplosionDamageMultiplier = 0f;

            foreach (PoisonSetBonusValues bonus in bonuses.Values)
            {
                DurationMultiplier = Mathf.Max(DurationMultiplier, bonus.DurationMultiplier);
                TickDamageMultiplier = Mathf.Max(TickDamageMultiplier, bonus.TickDamageMultiplier);
                SpreadOnDeath |= bonus.SpreadOnDeath;
                SpreadRadius = Mathf.Max(SpreadRadius, bonus.SpreadRadius);
                MaxSpreadTargets = Mathf.Max(MaxSpreadTargets, bonus.MaxSpreadTargets);
                SpreadDamageMultiplier = Mathf.Max(
                    SpreadDamageMultiplier,
                    bonus.SpreadDamageMultiplier);
                SpreadDuration = Mathf.Max(SpreadDuration, bonus.SpreadDuration);
                MaxSpreadStacks = Mathf.Max(MaxSpreadStacks, bonus.MaxSpreadStacks);
                ExplodeOnDeath |= bonus.ExplodeOnDeath;
                ExplosionRadius = Mathf.Max(ExplosionRadius, bonus.ExplosionRadius);
                ExplosionDamageMultiplier = Mathf.Max(
                    ExplosionDamageMultiplier,
                    bonus.ExplosionDamageMultiplier);
            }
        }

        private void HandleEnemyDied(EnemyBase defeatedEnemy)
        {
            if (isResolvingExplosion || defeatedEnemy == null ||
                (!SpreadOnDeath && !ExplodeOnDeath))
            {
                return;
            }

            StatusEffectManager defeatedStatus = defeatedEnemy.GetComponent<StatusEffectManager>();
            if (defeatedStatus == null ||
                !defeatedStatus.TryGetEffect(out PoisonEffect defeatedPoison))
            {
                return;
            }

            float searchRadius = Mathf.Max(
                SpreadOnDeath ? SpreadRadius : 0f,
                ExplodeOnDeath ? ExplosionRadius : 0f);
            List<EnemyBase> nearbyEnemies = FindNearbyEnemies(
                defeatedEnemy,
                Mathf.Max(0.1f, searchRadius));

            if (SpreadOnDeath)
            {
                int spreadCount = 0;
                foreach (EnemyBase enemy in nearbyEnemies)
                {
                    if (Vector2.Distance(enemy.transform.position, defeatedEnemy.transform.position) >
                        SpreadRadius)
                    {
                        continue;
                    }

                    StatusEffectManager statusEffectManager = enemy.GetComponent<StatusEffectManager>();
                    if (statusEffectManager == null) continue;

                    statusEffectManager.ConfigureCombatContext(playerManager);
                    statusEffectManager.ApplyEffect(new PoisonEffect(
                        defeatedPoison.BaseDamagePerStack * Mathf.Max(0f, SpreadDamageMultiplier),
                        Mathf.Max(0.1f, SpreadDuration),
                        Mathf.Min(defeatedPoison.StackCount, Mathf.Max(1, MaxSpreadStacks))));
                    spreadCount++;
                    if (spreadCount >= Mathf.Max(1, MaxSpreadTargets)) break;
                }
            }

            if (!ExplodeOnDeath) return;

            float explosionDamage = defeatedPoison.TickDamage *
                                    Mathf.Max(0f, ExplosionDamageMultiplier);
            if (explosionDamage <= 0f) return;

            try
            {
                isResolvingExplosion = true;
                foreach (EnemyBase enemy in nearbyEnemies)
                {
                    if (enemy.isDead ||
                        Vector2.Distance(enemy.transform.position, defeatedEnemy.transform.position) >
                        ExplosionRadius)
                    {
                        continue;
                    }
                    enemy.TakeDamage(explosionDamage, true);
                }
            }
            finally
            {
                isResolvingExplosion = false;
            }
        }

        private static List<EnemyBase> FindNearbyEnemies(EnemyBase origin, float radius)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(origin.transform.position, radius);
            HashSet<EnemyBase> uniqueEnemies = new HashSet<EnemyBase>();
            List<EnemyBase> result = new List<EnemyBase>();

            foreach (Collider2D hit in colliders)
            {
                EnemyBase enemy = hit != null ? hit.GetComponentInParent<EnemyBase>() : null;
                if (enemy == null || enemy == origin || enemy.isDead || !uniqueEnemies.Add(enemy))
                {
                    continue;
                }
                result.Add(enemy);
            }
            return result;
        }

        private void OnDestroy()
        {
            if (eventManager != null)
            {
                eventManager.OnEnemyDied -= HandleEnemyDied;
            }
        }
    }
}
