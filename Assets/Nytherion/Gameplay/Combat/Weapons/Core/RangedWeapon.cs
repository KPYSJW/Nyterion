using UnityEngine;
using Nytherion.Core.Managers;
using System.Collections;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Skills;

namespace Nytherion.GamePlay.Combat
{
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
        public string projectilePoolTag = "PlayerProj";

        protected GameObject currentProjectilePrefab;

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
        private WeaponCrossbowRecoil crossbowRecoil;

        public override void Initialize(WeaponData data)
        {
            base.Initialize(data);

            if (crossbowRecoil == null)
            {
                crossbowRecoil = GetComponent<WeaponCrossbowRecoil>();
            }
            crossbowRecoil?.CacheRestPose();

            if (data != null)
            {
                projectileSpeed = data.projectileSpeed;
                extraProjectileMode = data.extraProjectileMode;
                currentProjectilePrefab = data.projectilePrefab;
                
                if (firePoint != null)
                {
                    firePoint.localPosition = data.firePointOffset;
                }

                // 투사체 태그 업데이트 (프리팹 이름을 태그로 사용)
                if (currentProjectilePrefab != null)
                {
                    projectilePoolTag = currentProjectilePrefab.name;
                }
            }
            
            burstWait = new WaitForSeconds(burstInterval);
        }

        public GameObject SpawnProj(
            Vector2 direction,
            Vector3 spawnOffset = default,
            float chargePercent = 0f,
            float projectileDamageMultiplier = 1f)
        {
            Vector3 spawnPos = new Vector3(firePoint.position.x, firePoint.position.y, 0f) + spawnOffset;
            
            GameObject projectile;
            if (currentProjectilePrefab != null)
            {
                projectile = ObjectPoolManager.Instance.SpawnFromPool(currentProjectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                projectile = ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, spawnPos, Quaternion.identity);
            }

            if (projectile == null) return null;

            Vector2 normalizedDir = direction.normalized;
            float angle = Mathf.Atan2(normalizedDir.y, normalizedDir.x) * Mathf.Rad2Deg;
            if (weaponData != null)
            {
                angle += weaponData.projectileRotationOffset;
            }
            projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            //  Rigidbody2D가 있으면 속도 적용
            Rigidbody2D rb;
            if (projectile.TryGetComponent<Rigidbody2D>(out rb))
            {
                rb.velocity = normalizedDir * projectileSpeed;
            }
            
            // IProj 인터페이스를 구현한 별도 이동 스크립트가 있으면 속도 전달
            IProj iProj;
            if (projectile.TryGetComponent<IProj>(out iProj))
            {
                iProj.SetSpeed(projectileSpeed);
            }

            CombatModifierSnapshot currentSnapshot = playerManager != null && playerManager.playerRelicManager != null
                ? playerManager.playerRelicManager.CombatModifiers
                : CombatModifierSnapshot.Empty;

            bool shouldUseHoming = weaponData != null && weaponData.hasHomingProjectiles;
            shouldUseHoming |= currentSnapshot.HasProjectileHoming;

            HomingProj homingProjectile;
            if (!projectile.TryGetComponent(out homingProjectile) && shouldUseHoming)
            {
                homingProjectile = projectile.AddComponent<HomingProj>();
            }

            if (homingProjectile != null)
            {
                homingProjectile.SetHomingEnabled(shouldUseHoming, projectileSpeed);
            }
            
            if (projectile.TryGetComponent<CollisionObject>(out CollisionObject collisionObj))
            {
                if (weaponData != null)
                {
                    collisionObj.Configure(
                        weaponData.damage * EffectiveDamageMultiplier * projectileDamageMultiplier,
                        GetTraits(),
                        chargePercent,
                        weaponData.hitEffectPrefab,
                        currentSnapshot);
                }
            }

            SpriteRenderer projSprite;
            if (projectile.TryGetComponent<SpriteRenderer>(out projSprite))
            {
                SpriteRenderer weaponSprite;
                if (TryGetComponent<SpriteRenderer>(out weaponSprite))
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

        protected void FireProjectiles(
            Vector2 direction,
            int baseCount,
            float spreadAngle = 15f,
            float chargePercent = 0f,
            float projectileDamageMultiplier = 1f)
        {
            if (ShouldPlayFireAnimation())
            {
                PlayFireAnimation();
            }

            // 발사 이펙트 생성
            if (ShouldSpawnFireEffect() && firePoint != null && weaponData != null && weaponData.fireEffectPrefab != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                WeaponVFXHelper.PlayFireEffect(weaponData.fireEffectPrefab, firePoint.position, rotation, firePoint);
            }

            int extra = 0;
            if (playerManager != null && playerManager.currentPlayerData != null)
            {
                extra = Mathf.FloorToInt(playerManager.currentPlayerData.extraProjectiles);
            }
            int totalCount = baseCount + extra;

            if (totalCount <= 1)
            {
                SpawnProj(direction, default, chargePercent, projectileDamageMultiplier);
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
                            SpawnProj(spreadDirection, default, chargePercent, projectileDamageMultiplier);
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
                            SpawnProj(spreadDirection, default, chargePercent, projectileDamageMultiplier);
                        }
                    }
                }
                else if (extraProjectileMode == ExtraProjectileMode.Burst)
                {
                    StartCoroutine(FireBurstRoutine(direction, totalCount, chargePercent, projectileDamageMultiplier));
                }
                else if (extraProjectileMode == ExtraProjectileMode.Parallel)
                {
                    FireParallel(direction, totalCount, chargePercent, projectileDamageMultiplier);
                }
            }

            // 이벤트 발생 (쉐도우 클론 각인 등에서 사용)
            EventManager eventManager = playerManager != null ? playerManager.EventManager : null;
            if (eventManager != null && weaponData != null)
            {
                float baseDamage = weaponData.damage * EffectiveDamageMultiplier * projectileDamageMultiplier;
                eventManager.TriggerPlayerRangedAttack(direction, totalCount, baseDamage, firePoint, projectilePoolTag);
            }

            PlayWeaponRecoil(direction);
        }

        protected void PlayWeaponRecoil(Vector2 direction, float strength = 1f)
        {
            if (crossbowRecoil == null)
            {
                crossbowRecoil = GetComponent<WeaponCrossbowRecoil>();
            }

            crossbowRecoil?.Play(direction, strength);
        }

        private IEnumerator FireBurstRoutine(
            Vector2 direction,
            int totalCount,
            float chargePercent,
            float projectileDamageMultiplier)
        {
            for (int i = 0; i < totalCount; i++)
            {
                SpawnProj(direction, default, chargePercent, projectileDamageMultiplier);
                if (i < totalCount - 1)
                {
                    yield return burstWait;
                }
            }
        }

        private void FireParallel(
            Vector2 direction,
            int totalCount,
            float chargePercent,
            float projectileDamageMultiplier)
        {
            Vector2 perp = new Vector2(-direction.y, direction.x).normalized;
            float startOffset = -((totalCount - 1) * parallelSpacing) / 2f;

            for (int i = 0; i < totalCount; i++)
            {
                float currentOffset = startOffset + (i * parallelSpacing);
                Vector3 spawnOffset = new Vector3(perp.x, perp.y, 0f) * currentOffset;
                SpawnProj(direction, spawnOffset, chargePercent, projectileDamageMultiplier);
            }
        }

        protected virtual bool ShouldSpawnFireEffect() => true;
        protected virtual bool ShouldPlayFireAnimation() => true;
    }
}
