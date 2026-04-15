using UnityEngine;
using Nytherion.Core.Managers;
using UnityEditor.EditorTools;
using VContainer;

namespace Nytherion.GamePlay.Combat
{
    public abstract class RangedWeapon : WeaponBase
    {
        [Header("Ranged Settings")]
        public Transform firePoint;

        public string projectilePoolTag = "PlayerProjectile";

        private const float DefaultProjectileSpeed = 8f;

        private ObjectPoolManager poolManager;

        [Inject]
        public void Construct(ObjectPoolManager poolManager)
        {
            this.poolManager = poolManager;
        }

        public GameObject Projectile(Vector2 direction)
        {
            GameObject projectile = poolManager.SpawnFromPool(projectilePoolTag, firePoint.position, Quaternion.identity);

            if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
            {
                Vector2 normalizedDir = direction.normalized;
                rb.velocity = normalizedDir * DefaultProjectileSpeed;

                float angle = Mathf.Atan2(normalizedDir.y, normalizedDir.x) * Mathf.Rad2Deg;
                projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            if (projectile.TryGetComponent<CollisionObject>(out var collisionObj))
            {
                if (weaponData != null)
                {
                    collisionObj.damage = weaponData.damage;
                }
            }
            return projectile; 
        }
    }
}