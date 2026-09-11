using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("결정 탄도")]
    public sealed class CrystalBallisticsEffect : RelicEffectBase
    {
        [Min(0f)] public float shardDamageRatio = 0.35f;
        [Min(1)] public int maxShardTargets = 2;
        [Min(0.1f)] public float searchRadius = 7f;

        [NonSerialized] private CrystalBallisticsRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<CrystalBallisticsRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<CrystalBallisticsRuntime>();
            }
            appliedRuntime.SetBonus(this, shardDamageRatio, maxShardTargets, searchRadius);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<CrystalBallisticsRuntime>();
            }
            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public sealed class CrystalBallisticsRuntime : MonoBehaviour
    {
        private readonly Dictionary<CrystalBallisticsEffect, Vector3> bonuses =
            new Dictionary<CrystalBallisticsEffect, Vector3>();

        public float ShardDamageRatio { get; private set; }
        public int MaxShardTargets { get; private set; }
        public float SearchRadius { get; private set; } = 7f;

        public void SetBonus(CrystalBallisticsEffect source, float ratio, int targets, float radius)
        {
            if (source == null) return;
            bonuses[source] = new Vector3(ratio, targets, radius);
            Recalculate();
        }

        public void RemoveBonus(CrystalBallisticsEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        private void Recalculate()
        {
            ShardDamageRatio = 0f;
            MaxShardTargets = 0;
            SearchRadius = 7f;
            foreach (Vector3 bonus in bonuses.Values)
            {
                ShardDamageRatio = Mathf.Max(ShardDamageRatio, bonus.x);
                MaxShardTargets = Mathf.Max(MaxShardTargets, Mathf.RoundToInt(bonus.y));
                SearchRadius = Mathf.Max(SearchRadius, bonus.z);
            }
        }
    }

    [Serializable, RelicDisplayName("열충격")]
    public sealed class ThermalShockEffect : RelicEffectBase
    {
        [Min(0f)] public float damageRatio = 0.8f;
        [Min(0.1f)] public float explosionRadius = 3f;
        [Min(1)] public int maxTargets = 4;
        [Min(0f)] public float perTargetCooldown = 1f;

        [NonSerialized] private EventManager eventManager;
        [NonSerialized] private readonly Dictionary<int, float> nextTriggerTimes =
            new Dictionary<int, float>();

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            eventManager = playerManager != null ? playerManager.EventManager : null;
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed += HandleEnemyDamaged;
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed -= HandleEnemyDamaged;
            }
            eventManager = null;
            nextTriggerTimes.Clear();
        }

        private void HandleEnemyDamaged(PlayerDamageEventData eventData)
        {
            if (eventData.IsChainDamage || eventData.Target == null ||
                !eventData.Target.TryGetComponent(out StatusEffectManager statusManager) ||
                !statusManager.TryGetEffect(out FireEffect _) ||
                !statusManager.TryGetEffect(out IceEffect _))
            {
                return;
            }

            int targetId = eventData.Target.GetInstanceID();
            if (nextTriggerTimes.TryGetValue(targetId, out float nextTime) && Time.time < nextTime)
            {
                return;
            }
            nextTriggerTimes[targetId] = Time.time + Mathf.Max(0f, perTargetCooldown);

            DamageNearby(eventData.Target, eventData.DamageAmount * damageRatio);
        }

        private void DamageNearby(EnemyBase origin, float damage)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin.transform.position, explosionRadius);
            HashSet<EnemyBase> uniqueTargets = new HashSet<EnemyBase>();
            int count = 0;
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit != null ? hit.GetComponentInParent<EnemyBase>() : null;
                if (enemy == null || enemy == origin || enemy.isDead || !uniqueTargets.Add(enemy))
                {
                    continue;
                }

                enemy.TakeDamage(Mathf.Max(0f, damage), true);
                count++;
                if (count >= Mathf.Max(1, maxTargets)) break;
            }
        }
    }

    [Serializable, RelicDisplayName("부식성 화염")]
    public sealed class CorrosiveFlameEffect : RelicEffectBase
    {
        [Min(0f)] public float spreadFireDamageRatio = 0.5f;
        [Min(0.1f)] public float spreadRadius = 5f;
        [Min(1)] public int maxTargets = 3;
        [Min(0.1f)] public float fireDuration = 4f;

        [NonSerialized] private PlayerManager playerManager;
        [NonSerialized] private EventManager eventManager;

        public override void ApplyEffect(PlayerManager currentPlayerManager, int level)
        {
            RemoveEffect(currentPlayerManager, level);
            playerManager = currentPlayerManager;
            eventManager = currentPlayerManager != null ? currentPlayerManager.EventManager : null;
            if (eventManager != null)
            {
                eventManager.OnEnemyDied += HandleEnemyDied;
            }
        }

        public override void RemoveEffect(PlayerManager currentPlayerManager, int level)
        {
            if (eventManager != null)
            {
                eventManager.OnEnemyDied -= HandleEnemyDied;
            }
            eventManager = null;
            playerManager = null;
        }

        private void HandleEnemyDied(EnemyBase defeatedEnemy)
        {
            if (defeatedEnemy == null ||
                !defeatedEnemy.TryGetComponent(out StatusEffectManager defeatedStatus) ||
                !defeatedStatus.TryGetEffect(out FireEffect fire) ||
                !defeatedStatus.TryGetEffect(out PoisonEffect poison))
            {
                return;
            }

            float damage = fire.TickDamage * Mathf.Max(0f, spreadFireDamageRatio) *
                           (1f + 0.08f * poison.StackCount);
            Collider2D[] hits = Physics2D.OverlapCircleAll(defeatedEnemy.transform.position, spreadRadius);
            HashSet<EnemyBase> uniqueTargets = new HashSet<EnemyBase>();
            int count = 0;
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit != null ? hit.GetComponentInParent<EnemyBase>() : null;
                if (enemy == null || enemy == defeatedEnemy || enemy.isDead || !uniqueTargets.Add(enemy) ||
                    !enemy.TryGetComponent(out StatusEffectManager statusManager))
                {
                    continue;
                }

                statusManager.ConfigureCombatContext(playerManager);
                statusManager.ApplyEffect(new FireEffect(damage, fireDuration));
                count++;
                if (count >= Mathf.Max(1, maxTargets)) break;
            }
        }
    }

    [Serializable, RelicDisplayName("피의 성채")]
    public sealed class BloodFortressEffect : RelicEffectBase
    {
        [Range(0f, 1f)] public float recoveredDamageRatio = 0.15f;

        [NonSerialized] private PlayerHealth playerHealth;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            playerHealth = playerManager != null ? playerManager.playerHealth : null;
            if (playerHealth != null)
            {
                PlayerHealth.OnPlayerDamaged += HandlePlayerDamaged;
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            PlayerHealth.OnPlayerDamaged -= HandlePlayerDamaged;
            playerHealth = null;
        }

        private void HandlePlayerDamaged(float damage)
        {
            playerHealth?.Heal(damage * Mathf.Clamp01(recoveredDamageRatio));
        }
    }

    [Serializable, RelicDisplayName("뇌광 추적")]
    public sealed class DashChainLightningEffect : RelicEffectBase
    {
        [Min(0f)] public float damageRatio = 0.45f;
        [Min(0.1f)] public float searchRadius = 6f;
        [Min(1)] public int maxTargets = 4;
        [Min(0.1f)] public float hitWaitDuration = 2f;

        [NonSerialized] private EventManager eventManager;
        [NonSerialized] private bool dashArmed;
        [NonSerialized] private bool waitingForRangedHit;
        [NonSerialized] private float hitWaitUntil;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            eventManager = playerManager != null ? playerManager.EventManager : null;
            if (eventManager == null) return;

            eventManager.OnPlayerDashStarted += HandleDashStarted;
            eventManager.OnPlayerRangedAttack += HandleRangedAttack;
            eventManager.OnEnemyDamagedByPlayerDetailed += HandleEnemyDamaged;
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (eventManager != null)
            {
                eventManager.OnPlayerDashStarted -= HandleDashStarted;
                eventManager.OnPlayerRangedAttack -= HandleRangedAttack;
                eventManager.OnEnemyDamagedByPlayerDetailed -= HandleEnemyDamaged;
            }
            eventManager = null;
            dashArmed = false;
            waitingForRangedHit = false;
        }

        private void HandleDashStarted()
        {
            dashArmed = true;
            waitingForRangedHit = false;
        }

        private void HandleRangedAttack(Vector2 _, int __, float ___, Transform ____, string _____)
        {
            if (!dashArmed) return;
            dashArmed = false;
            waitingForRangedHit = true;
            hitWaitUntil = Time.time + Mathf.Max(0.1f, hitWaitDuration);
        }

        private void HandleEnemyDamaged(PlayerDamageEventData eventData)
        {
            if (!waitingForRangedHit || Time.time > hitWaitUntil || eventData.IsChainDamage ||
                eventData.Target == null)
            {
                if (Time.time > hitWaitUntil) waitingForRangedHit = false;
                return;
            }

            waitingForRangedHit = false;
            Collider2D[] hits = Physics2D.OverlapCircleAll(eventData.Target.transform.position, searchRadius);
            HashSet<EnemyBase> uniqueTargets = new HashSet<EnemyBase>();
            int count = 0;
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit != null ? hit.GetComponentInParent<EnemyBase>() : null;
                if (enemy == null || enemy == eventData.Target || enemy.isDead ||
                    !uniqueTargets.Add(enemy))
                {
                    continue;
                }

                enemy.TakeDamage(eventData.DamageAmount * Mathf.Max(0f, damageRatio), true);
                if (!enemy.isDead && enemy.TryGetComponent(out StatusEffectManager statusManager))
                {
                    statusManager.ApplyEffect(new LightningEffect(3f));
                }
                count++;
                if (count >= Mathf.Max(1, maxTargets)) break;
            }
        }
    }

    [Serializable, RelicDisplayName("황금 질주")]
    public sealed class GoldenRushEffect : RelicEffectBase
    {
        [Min(0.1f)] public float goldWindow = 1.5f;
        [Min(0f)] public float goldGainBonus = 0.25f;
        [Min(0f)] public float dashCooldownRefund = 0.15f;

        [NonSerialized] private EventManager eventManager;
        [NonSerialized] private CurrencyDataManager currencyDataManager;
        [NonSerialized] private PlayerController playerController;
        [NonSerialized] private float goldWindowEnd;
        [NonSerialized] private bool isGrantingBonus;
        [NonSerialized] private bool refundedThisDash;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            eventManager = playerManager.EventManager;
            playerController = playerManager.GetComponent<PlayerController>();
            currencyDataManager = DataLifetimeScope.Instance != null
                ? DataLifetimeScope.Instance.GetDataManager<CurrencyDataManager>()
                : null;

            if (eventManager != null)
            {
                eventManager.OnPlayerDashStarted += HandleDashStarted;
            }
            if (currencyDataManager != null)
            {
                currencyDataManager.OnDataChanged += HandleCurrencyChanged;
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (eventManager != null)
            {
                eventManager.OnPlayerDashStarted -= HandleDashStarted;
            }
            if (currencyDataManager != null)
            {
                currencyDataManager.OnDataChanged -= HandleCurrencyChanged;
            }
            eventManager = null;
            currencyDataManager = null;
            playerController = null;
        }

        private void HandleDashStarted()
        {
            goldWindowEnd = Time.time + Mathf.Max(0.1f, goldWindow);
            refundedThisDash = false;
        }

        private void HandleCurrencyChanged(CurrencyChangeData data)
        {
            if (isGrantingBonus || Time.time > goldWindowEnd || data.isSilent ||
                data.currencyType != CurrencyType.Gold || data.changeAmount <= 0 ||
                currencyDataManager == null)
            {
                return;
            }

            int bonus = Mathf.RoundToInt(data.changeAmount * Mathf.Max(0f, goldGainBonus));
            if (bonus > 0)
            {
                try
                {
                    isGrantingBonus = true;
                    currencyDataManager.AddUnmodifiedCurrency(CurrencyType.Gold, bonus);
                }
                finally
                {
                    isGrantingBonus = false;
                }
            }

            if (!refundedThisDash && playerController != null)
            {
                playerController.LastDashTime -= Mathf.Max(0f, dashCooldownRefund);
                refundedThisDash = true;
            }
        }
    }

    [Serializable, RelicDisplayName("군단 소환수 강화")]
    public sealed class LegionCompanionEffect : RelicEffectBase
    {
        [Min(0f)] public float damageMultiplier = 1.25f;
        [Range(0.1f, 1f)] public float attackIntervalMultiplier = 0.85f;

        [NonSerialized] private LegionCompanionRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<LegionCompanionRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<LegionCompanionRuntime>();
            }
            appliedRuntime.SetBonus(this, damageMultiplier, attackIntervalMultiplier);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<LegionCompanionRuntime>();
            }
            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public sealed class LegionCompanionRuntime : MonoBehaviour
    {
        private readonly Dictionary<LegionCompanionEffect, Vector2> bonuses =
            new Dictionary<LegionCompanionEffect, Vector2>();

        public float DamageMultiplier { get; private set; } = 1f;
        public float AttackIntervalMultiplier { get; private set; } = 1f;

        public void SetBonus(LegionCompanionEffect source, float damage, float interval)
        {
            if (source == null) return;
            bonuses[source] = new Vector2(damage, interval);
            Recalculate();
        }

        public void RemoveBonus(LegionCompanionEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        private void Recalculate()
        {
            DamageMultiplier = 1f;
            AttackIntervalMultiplier = 1f;
            foreach (Vector2 bonus in bonuses.Values)
            {
                DamageMultiplier = Mathf.Max(DamageMultiplier, bonus.x);
                AttackIntervalMultiplier = Mathf.Min(AttackIntervalMultiplier, bonus.y);
            }
        }
    }
}
