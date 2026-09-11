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
    /// 냉기 중첩을 빙결로 전환하고, 빙결된 적을 공격하면 파쇄 피해를 발생시킨다.
    /// </summary>
    [Serializable, RelicDisplayName("빙결 파수꾼 세트 강화")]
    public sealed class FrostSetBonusEffect : RelicEffectBase
    {
        [Min(1)] public int requiredStacksToFreeze = 4;
        [Min(0f)] public float freezeDuration = 0.5f;

        [Header("파쇄")]
        public bool enableShatter;
        [Min(0f)] public float shatterDamageRatio = 0.5f;
        [Min(0.1f)] public float shatterRadius = 2.5f;
        [Min(1)] public int maxShatterTargets = 3;

        [NonSerialized] private FrostSetBonusRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<FrostSetBonusRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<FrostSetBonusRuntime>();
            }

            appliedRuntime.SetBonus(this, playerManager, new FrostSetBonusValues
            {
                RequiredStacksToFreeze = requiredStacksToFreeze,
                FreezeDuration = freezeDuration,
                EnableShatter = enableShatter,
                ShatterDamageRatio = shatterDamageRatio,
                ShatterRadius = shatterRadius,
                MaxShatterTargets = maxShatterTargets
            });
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<FrostSetBonusRuntime>();
            }

            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public struct FrostSetBonusValues
    {
        public int RequiredStacksToFreeze;
        public float FreezeDuration;
        public bool EnableShatter;
        public float ShatterDamageRatio;
        public float ShatterRadius;
        public int MaxShatterTargets;
    }

    public sealed class FrostSetBonusRuntime : MonoBehaviour
    {
        private readonly Dictionary<FrostSetBonusEffect, FrostSetBonusValues> bonuses =
            new Dictionary<FrostSetBonusEffect, FrostSetBonusValues>();

        private PlayerManager playerManager;
        private EventManager eventManager;

        public int RequiredStacksToFreeze { get; private set; } = int.MaxValue;
        public float FreezeDuration { get; private set; }
        private bool EnableShatter { get; set; }
        private float ShatterDamageRatio { get; set; }
        private float ShatterRadius { get; set; }
        private int MaxShatterTargets { get; set; }

        public void SetBonus(
            FrostSetBonusEffect source,
            PlayerManager currentPlayerManager,
            FrostSetBonusValues values)
        {
            if (source == null || currentPlayerManager == null) return;

            bonuses[source] = values;
            ConfigurePlayer(currentPlayerManager);
            Recalculate();
        }

        public void RemoveBonus(FrostSetBonusEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        public void ApplyTo(IceEffect iceEffect)
        {
            if (iceEffect == null || RequiredStacksToFreeze == int.MaxValue) return;
            iceEffect.ApplySetBonus(RequiredStacksToFreeze, FreezeDuration);
        }

        private void ConfigurePlayer(PlayerManager currentPlayerManager)
        {
            playerManager = currentPlayerManager;
            EventManager newEventManager = currentPlayerManager.EventManager;
            if (eventManager == newEventManager) return;

            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed -= HandleEnemyDamaged;
            }

            eventManager = newEventManager;
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed += HandleEnemyDamaged;
            }
        }

        private void Recalculate()
        {
            RequiredStacksToFreeze = int.MaxValue;
            FreezeDuration = 0f;
            EnableShatter = false;
            ShatterDamageRatio = 0f;
            ShatterRadius = 0f;
            MaxShatterTargets = 0;

            foreach (FrostSetBonusValues bonus in bonuses.Values)
            {
                RequiredStacksToFreeze = Mathf.Min(
                    RequiredStacksToFreeze,
                    Mathf.Max(1, bonus.RequiredStacksToFreeze));
                FreezeDuration = Mathf.Max(FreezeDuration, bonus.FreezeDuration);
                EnableShatter |= bonus.EnableShatter;
                ShatterDamageRatio = Mathf.Max(ShatterDamageRatio, bonus.ShatterDamageRatio);
                ShatterRadius = Mathf.Max(ShatterRadius, bonus.ShatterRadius);
                MaxShatterTargets = Mathf.Max(MaxShatterTargets, bonus.MaxShatterTargets);
            }
        }

        private void HandleEnemyDamaged(PlayerDamageEventData eventData)
        {
            if (!EnableShatter || eventData.IsChainDamage || eventData.Target == null ||
                !eventData.Target.TryGetComponent(out StatusEffectManager statusManager) ||
                !statusManager.TryGetEffect(out IceEffect iceEffect) ||
                !iceEffect.ConsumeFreeze())
            {
                return;
            }

            Vector3 center = eventData.Target.transform.position;
            float shatterDamage = eventData.DamageAmount * Mathf.Max(0f, ShatterDamageRatio);
            DamageNearbyEnemies(
                eventData.Target,
                center,
                Mathf.Max(0.1f, ShatterRadius),
                Mathf.Max(1, MaxShatterTargets),
                shatterDamage,
                false);

            if (playerManager != null &&
                playerManager.TryGetComponent(out CrystalBallisticsRuntime crystalBallistics))
            {
                DamageNearbyEnemies(
                    eventData.Target,
                    center,
                    crystalBallistics.SearchRadius,
                    crystalBallistics.MaxShardTargets,
                    eventData.DamageAmount * crystalBallistics.ShardDamageRatio,
                    true);
            }
        }

        private void DamageNearbyEnemies(
            EnemyBase origin,
            Vector3 center,
            float radius,
            int maxTargets,
            float damage,
            bool applyIce)
        {
            if (damage <= 0f) return;

            Collider2D[] colliders = Physics2D.OverlapCircleAll(center, radius);
            HashSet<EnemyBase> uniqueEnemies = new HashSet<EnemyBase>();
            int hitCount = 0;
            foreach (Collider2D hit in colliders)
            {
                EnemyBase enemy = hit != null ? hit.GetComponentInParent<EnemyBase>() : null;
                if (enemy == null || enemy == origin || enemy.isDead || !uniqueEnemies.Add(enemy))
                {
                    continue;
                }

                enemy.TakeDamage(damage, true);
                if (applyIce && !enemy.isDead &&
                    enemy.TryGetComponent(out StatusEffectManager statusManager))
                {
                    statusManager.ConfigureCombatContext(playerManager);
                    statusManager.ApplyEffect(new IceEffect(3f));
                }

                hitCount++;
                if (hitCount >= maxTargets) break;
            }
        }

        private void OnDestroy()
        {
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed -= HandleEnemyDamaged;
            }
        }
    }
}
