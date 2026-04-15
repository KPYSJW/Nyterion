using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Combat;
using VContainer;
using UnityEditor.EditorTools;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class MeteorWeapon : WeaponBase
    {
        [Header("Meteor Settings")]
        public string meteorPoolTag = "Meteor";

        public float dropHeight = 5f;

        private ObjectPoolManager poolManager;

        [Inject]
        public void Construct(ObjectPoolManager poolManager)
        {
            this.poolManager = poolManager;
        }
        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            lastAttackTime = Time.time;

            Vector3 spawnPos = targetPosition + (Vector3.up * dropHeight);

            GameObject meteor = poolManager.SpawnFromPool(meteorPoolTag, spawnPos, Quaternion.identity);

            if (meteor.TryGetComponent<MeteorProjectile>(out var proj))
            {
                proj.Initialize(targetPosition);
            }

            if (meteor.TryGetComponent<Nytherion.GamePlay.Combat.CollisionObject>(out var col))
            {
                col.damage = weaponData.damage;
            }
        }

        public override void AttackEnd()
        {
        }
    }
}