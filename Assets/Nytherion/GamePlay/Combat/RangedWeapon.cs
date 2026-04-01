using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public abstract class RangedWeapon : WeaponBase
    {
        [Header("Ranged Settings")]
        public Transform firePoint;

        public string projectilePoolTag = "PlayerProjectile";

        private const float DefaultProjectileSpeed = 8f;

        public GameObject Projectile(Vector2 direction)
        {
            GameObject projectile = ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, firePoint.position, Quaternion.identity);

            if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
            {
                Vector2 normalizedDir = direction.normalized;
                rb.velocity = normalizedDir * DefaultProjectileSpeed;

                float angle = Mathf.Atan2(normalizedDir.y, normalizedDir.x) * Mathf.Rad2Deg;
                projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            return projectile; 
        }
    }
}