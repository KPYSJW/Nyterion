using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class MeteorWeapon : WeaponBase
    {
        [Header("Meteor Settings")]
        public string meteorPoolTag = "Meteor";

        public float dropHeight = 5f;

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            lastAttackTime = Time.time;

            Vector3 spawnPos = targetPosition + (Vector3.up * dropHeight);

            GameObject meteor = ObjectPoolManager.Instance.SpawnFromPool(meteorPoolTag, spawnPos, Quaternion.identity);

            if (meteor.TryGetComponent<MeteorProjectile>(out var proj))
            {
                proj.Initialize(targetPosition);
            }

            if (meteor.TryGetComponent<CollisionObject>(out var col))
            {
                col.damage = weaponData.damage;
            }
        }

        public override void AttackEnd()
        {
        }
    }
}