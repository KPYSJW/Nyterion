using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("풍석 적중 투사체")]
    public class WindStoneRelicEffect : RelicEffectBase
    {
        public GameObject projectilePrefab;
        [Range(0f, 1f)] public float procChance = 0.2f;
        [Min(0f)] public float damageRatio = 0.5f;
        [Min(0f)] public float damageRatioPerLevel = 0.1f;
        [Min(0f)] public float spawnRadius = 0.65f;
        [Min(0.1f)] public float projectileSpeed = 6f;

        [NonSerialized] private PlayerManager player;
        [NonSerialized] private EventManager events;
        [NonSerialized] private int currentLevel;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            player = playerManager;
            currentLevel = Mathf.Max(1, level);
            events = player != null ? player.EventManager : null;
            if (events != null) events.OnEnemyDamagedByPlayerDetailed += HandleHit;
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (events != null) events.OnEnemyDamagedByPlayerDetailed -= HandleHit;
            events = null;
            player = null;
        }

        private void HandleHit(PlayerDamageEventData hit)
        {
            if (hit.IsChainDamage || hit.Target == null || hit.DamageAmount <= 0f ||
                player == null || player.ObjectPool == null || projectilePrefab == null ||
                UnityEngine.Random.value >= Mathf.Clamp01(procChance)) return;

            Vector2 direction = player.PlayerCombat != null
                ? player.PlayerCombat.LastAttackDirection
                : (Vector2)(hit.Target.transform.position - player.transform.position);
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector3 position = player.transform.position + (Vector3)(UnityEngine.Random.insideUnitCircle * spawnRadius);
            GameObject projectile = player.ObjectPool.SpawnFromPool(projectilePrefab, position,
                Quaternion.identity);
            if (projectile == null) return;
            float size = player.currentPlayerData != null
                ? Mathf.Max(0.01f, player.currentPlayerData.projectileSizeMultiplier) : 1f;
            projectile.transform.localScale *= size;
            if (projectile.TryGetComponent(out Rigidbody2D body)) body.velocity = direction * projectileSpeed;
            if (projectile.TryGetComponent(out CollisionObject collision))
            {
                collision.poolTag = projectilePrefab.name;
                collision.Configure(hit.DamageAmount * Mathf.Max(0f, damageRatio + (currentLevel - 1) * damageRatioPerLevel),
                    new List<EquipmentTrait>(), 0f, collision.hitEffectPrefab, CombatModifierSnapshot.Empty, true);
                collision.effectSizeMultiplier = size;
            }
        }
    }
}
