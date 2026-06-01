using UnityEngine;
using Nytherion.Core.Managers;
using System.Collections;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat
{
    public interface IProjectile
    {
        void SetSpeed(float speed);
    }

    public enum ExtraProjectileMode
    {
        Spread,
        Burst,
        Parallel
    }

    public abstract class RangedWeapon : WeaponBase
    {
        [Header("Ranged Settings")]
        public Transform firePoint;

        [HideInInspector]
        public string projectilePoolTag = "PlayerProjectile";

        [Header("Extra Projectile Settings")]
        [Tooltip("추가 투사체 발사 방식")]
        public ExtraProjectileMode extraProjectileMode = ExtraProjectileMode.Spread;
        
        [Tooltip("Burst 모드일 때 투사체 간 발사 간격")]
        public float burstInterval = 0.05f;
        
        [Tooltip("Parallel 모드일 때 투사체 간 간격")]
        public float parallelSpacing = 0.5f;

        [Tooltip("투사체의 날아가는 속도")]
        public float projectileSpeed = 8f;

        private WaitForSeconds burstWait;

        public override void Initialize(WeaponData data)
        {
            base.Initialize(data);

            if (data != null)
            {
                projectileSpeed = data.projectileSpeed;
                extraProjectileMode = data.extraProjectileMode;
                
                if (firePoint != null)
                {
                    firePoint.localPosition = data.firePointOffset;
                }

                // 투사체 태그 업데이트 (프리팹 이름을 태그로 사용)
                if (data.projectilePrefab != null)
                {
                    projectilePoolTag = data.projectilePrefab.name;
                }
            }
            
            burstWait = new WaitForSeconds(burstInterval);
        }

        public GameObject Projectile(Vector2 direction, Vector3 spawnOffset = default)
        {
            Vector3 spawnPos = new Vector3(firePoint.position.x, firePoint.position.y, 0f) + spawnOffset;
            
            GameObject projectile;
            if (weaponData != null && weaponData.projectilePrefab != null)
            {
                projectile = ObjectPoolManager.Instance.SpawnFromPool(weaponData.projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                projectile = ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, spawnPos, Quaternion.identity);
            }

            if (projectile == null) return null;

            Vector2 normalizedDir = direction.normalized;
            float angle = Mathf.Atan2(normalizedDir.y, normalizedDir.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            //  Rigidbody2D가 있으면 속도 적용
            if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.velocity = normalizedDir * projectileSpeed;
            }
            
            //  IProjectile 인터페이스를 구현한 별도 이동 스크립트가 있으면 속도 전달
            if (projectile.TryGetComponent<IProjectile>(out var iProj))
            {
                iProj.SetSpeed(projectileSpeed);
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
            PlayFireAnimation();

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
                if (extraProjectileMode == ExtraProjectileMode.Spread)
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
                else if (extraProjectileMode == ExtraProjectileMode.Burst)
                {
                    StartCoroutine(FireBurstRoutine(direction, totalCount));
                }
                else if (extraProjectileMode == ExtraProjectileMode.Parallel)
                {
                    FireParallel(direction, totalCount);
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

        private IEnumerator FireBurstRoutine(Vector2 direction, int totalCount)
        {
            for (int i = 0; i < totalCount; i++)
            {
                Projectile(direction);
                if (i < totalCount - 1)
                {
                    yield return burstWait;
                }
            }
        }

        private void FireParallel(Vector2 direction, int totalCount)
        {
            Vector2 perp = new Vector2(-direction.y, direction.x).normalized;
            float startOffset = -((totalCount - 1) * parallelSpacing) / 2f;

            for (int i = 0; i < totalCount; i++)
            {
                float currentOffset = startOffset + (i * parallelSpacing);
                Vector3 spawnOffset = new Vector3(perp.x, perp.y, 0f) * currentOffset;
                Projectile(direction, spawnOffset);
            }
        }
    }
}