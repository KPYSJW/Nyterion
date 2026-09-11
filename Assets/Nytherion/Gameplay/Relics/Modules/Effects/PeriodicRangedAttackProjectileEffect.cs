using System;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 일정 횟수의 원거리 공격마다 현재 무기의 투사체를 추가로 발사한다.
    /// </summary>
    [Serializable, RelicDisplayName("주기적 추가 투사체")]
    public class PeriodicRangedAttackProjectileEffect : RelicEffectBase
    {
        [Min(1)]
        [Tooltip("추가 투사체가 발사되는 공격 간격")]
        public int attackInterval = 5;

        [Min(1)]
        [Tooltip("조건을 만족했을 때 추가로 발사할 투사체 수")]
        public int additionalProjectileCount = 1;

        [Min(0f)]
        [Tooltip("추가 투사체의 원래 공격 대비 피해 배율")]
        public float damageMultiplier = 1f;

        [Min(0f)]
        [Tooltip("여러 투사체가 발사될 때 적용할 전체 탄퍼짐 각도")]
        public float spreadAngle = 12f;

        [NonSerialized] private PlayerManager cachedPlayerManager;
        [NonSerialized] private EventManager cachedEventManager;
        [NonSerialized] private int attackCount;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            cachedPlayerManager = playerManager;
            cachedEventManager = playerManager.EventManager;
            attackCount = 0;
            if (cachedEventManager == null) return;

            cachedEventManager.OnPlayerRangedAttack -= HandleRangedAttack;
            cachedEventManager.OnPlayerRangedAttack += HandleRangedAttack;
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (cachedEventManager != null)
            {
                cachedEventManager.OnPlayerRangedAttack -= HandleRangedAttack;
            }
            cachedEventManager = null;
            cachedPlayerManager = null;
            attackCount = 0;
        }

        private void HandleRangedAttack(
            Vector2 direction,
            int projectileCount,
            float baseDamage,
            Transform firePoint,
            string poolTag)
        {
            attackCount++;
            if (attackCount < Mathf.Max(1, attackInterval)) return;
            attackCount = 0;

            if (cachedPlayerManager == null || firePoint == null ||
                ObjectPoolManager.Instance == null || string.IsNullOrEmpty(poolTag) ||
                direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            WeaponBase currentWeapon = cachedPlayerManager.PlayerCombat != null
                ? cachedPlayerManager.PlayerCombat.currentWeapon
                : null;
            RangedWeapon rangedWeapon = currentWeapon as RangedWeapon;
            WeaponData weaponData = currentWeapon != null ? currentWeapon.weaponData : null;
            float projectileSpeed = rangedWeapon != null ? rangedWeapon.projectileSpeed : 8f;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int count = Mathf.Max(1, additionalProjectileCount);
            float angleStep = count > 1 ? spreadAngle / (count - 1) : 0f;
            float startOffset = count > 1 ? -spreadAngle * 0.5f : 0f;

            List<Nytherion.Core.Enums.EquipmentTrait> traits = currentWeapon != null
                ? new List<Nytherion.Core.Enums.EquipmentTrait>(currentWeapon.GetTraits())
                : new List<Nytherion.Core.Enums.EquipmentTrait>();
            CombatModifierSnapshot modifiers = cachedPlayerManager.playerRelicManager != null
                ? cachedPlayerManager.playerRelicManager.CombatModifiers
                : CombatModifierSnapshot.Empty;

            for (int i = 0; i < count; i++)
            {
                float currentAngle = baseAngle + startOffset + angleStep * i;
                Vector2 projectileDirection = new Vector2(
                    Mathf.Cos(currentAngle * Mathf.Deg2Rad),
                    Mathf.Sin(currentAngle * Mathf.Deg2Rad));
                float rotationOffset = weaponData != null ? weaponData.projectileRotationOffset : 0f;
                GameObject projectile = ObjectPoolManager.Instance.SpawnFromPool(
                    poolTag,
                    firePoint.position,
                    Quaternion.AngleAxis(currentAngle + rotationOffset, Vector3.forward));
                if (projectile == null) continue;

                if (projectile.TryGetComponent(out Rigidbody2D rigidbody))
                {
                    rigidbody.velocity = projectileDirection * projectileSpeed;
                }
                if (projectile.TryGetComponent(out IProj projectileController))
                {
                    projectileController.SetSpeed(projectileSpeed);
                }
                if (projectile.TryGetComponent(out CollisionObject collisionObject))
                {
                    collisionObject.poolTag = poolTag;
                    collisionObject.Configure(
                        baseDamage * Mathf.Max(0f, damageMultiplier),
                        new List<Nytherion.Core.Enums.EquipmentTrait>(traits),
                        0f,
                        weaponData != null ? weaponData.hitEffectPrefab : null,
                        modifiers);
                }
            }
        }
    }
}
