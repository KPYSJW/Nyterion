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
            Vector3 spawnPos = new Vector3(firePoint.position.x, firePoint.position.y, 0f);
            GameObject projectile = ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, spawnPos, Quaternion.identity);

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
                    collisionObj.damage = weaponData.damage * damageMultiplier;
                }
            }

            if (projectile.TryGetComponent<SpriteRenderer>(out var projSprite))
            {
                if (TryGetComponent<SpriteRenderer>(out var weaponSprite))
                {
                    projSprite.sortingLayerID = weaponSprite.sortingLayerID;
                    projSprite.sortingOrder = weaponSprite.sortingOrder + 1; 
                }
                else
                {
                    projSprite.sortingOrder = 10; 
                }
            }

            return projectile; 
        }

        protected void FireProjectiles(Vector2 direction, int baseCount, float spreadAngle = 15f)
        {
            int extra = 0;
            if (playerManager != null && playerManager.currentPlayerData != null)
            {
                extra = Mathf.FloorToInt(playerManager.currentPlayerData.extraProjectiles);
            }
            int totalCount = baseCount + extra;

            if (totalCount <= 1)
            {
                Projectile(direction);
            }
            else
            {
                float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                
                if (totalCount == 2)
                {
                    float startAngle = baseAngle - (spreadAngle / 2f);
                    for (int i = 0; i < 2; i++)
                    {
                        float currentAngle = startAngle + (spreadAngle * i);
                        Vector2 spreadDirection = new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad));
                        Projectile(spreadDirection);
                    }
                }
                else
                {
                    float startAngle = baseAngle - (spreadAngle / 2f);
                    float angleStep = spreadAngle / (totalCount - 1);

                    for (int i = 0; i < totalCount; i++)
                    {
                        float currentAngle = startAngle + (angleStep * i);
                        Vector2 spreadDirection = new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad));
                        Projectile(spreadDirection);
                    }
                }
            }

            // 이벤트 발생 (쉐도우 클론 각인 등에서 사용)
            var eventManager = GameObject.FindObjectOfType<EventManager>();
            if (eventManager != null && weaponData != null)
            {
                float baseDamage = weaponData.damage * damageMultiplier;
                eventManager.TriggerPlayerRangedAttack(direction, totalCount, baseDamage, firePoint, projectilePoolTag);
            }
        }
    }
}