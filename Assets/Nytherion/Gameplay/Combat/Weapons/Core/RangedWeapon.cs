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
        private int nextProjectileAnimationIndex;

        public override void Initialize(WeaponData data)
        {
            base.Initialize(data);
            nextProjectileAnimationIndex = 0;

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
            float projectileDamageMultiplier = 1f,
            Collider2D homingTarget = null,
            bool targetSelected = false)
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

            Vector2 normalizedDir = GetProjectileDirection(direction).normalized;
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
                if (!targetSelected && shouldUseHoming)
                {
                    homingTarget = HomingProj.FindClosestEnemy(spawnPos,
                        weaponData != null ? weaponData.homingSearchRadius : 10f);
                }
                homingProjectile.Initialize(shouldUseHoming, projectileSpeed, normalizedDir, homingTarget,
                    weaponData != null ? weaponData.homingTurnSpeed : 180f,
                    weaponData != null ? weaponData.homingLaunchDuration : 0.15f,
                    weaponData != null ? weaponData.projectileRotationOffset : 0f);
            }

            if (weaponData != null && (shouldUseHoming || weaponData.useHomingLaunchAngles))
            {
                if (!projectile.TryGetComponent(out AutoReturnToPool lifetime))
                    lifetime = projectile.AddComponent<AutoReturnToPool>();
                lifetime.InitializeDelay(weaponData.homingLifetime);
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

            ConfigureSpawnedProjectile(projectile, normalizedDir, chargePercent);
            ApplyProjectileAnimation(projectile);
            // 풀은 발사마다 원본 스케일을 복원합니다. 차징 보정 뒤에 한 번만 적용합니다.
            projectile.transform.localScale *= EffectSizeMultiplier;
            if (collisionObj != null) collisionObj.effectSizeMultiplier = EffectSizeMultiplier;
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
            int totalCount = Mathf.Max(1, baseCount) + Mathf.Max(0, extra);

            bool useLaunchAngles = weaponData != null && weaponData.useHomingLaunchAngles &&
                weaponData.homingLaunchAngles != null && weaponData.homingLaunchAngles.Length > 0;
            if (useLaunchAngles)
            {
                // 한 발사 묶음의 목표를 한 번 선택하고 모든 탄이 같은 목표를 향해 출발합니다.
                Collider2D target = HomingProj.FindClosestEnemy(firePoint.position, weaponData.homingSearchRadius);
                Vector2 basis = target != null
                    ? (Vector2)target.bounds.center - (Vector2)firePoint.position
                    : direction;
                if (basis.sqrMagnitude < 0.0001f) basis = direction;
                if (basis.sqrMagnitude < 0.0001f) basis = firePoint.right;
                float baseAngle = Mathf.Atan2(basis.y, basis.x) * Mathf.Rad2Deg;
                // 각도 배열은 발사 배치만 정의하며 발사 수는 기존 기본 수 + 증가 효과를 따릅니다.
                totalCount = Mathf.Max(1, baseCount) + Mathf.Max(0, extra);
                int angleCount = weaponData.homingLaunchAngles.Length;
                float centerIndex = (angleCount - 1) * 0.5f;
                float halfWidth = Mathf.Min((totalCount - 1) * 0.5f, centerIndex);
                for (int i = 0; i < totalCount; i++)
                {
                    float offset = 0f;
                    if (totalCount > 1)
                    {
                        // 탄 수가 늘면 중앙부터 폭을 넓히고, 배열보다 많아도 최대 발사 폭을 유지합니다.
                        float sample = centerIndex + Mathf.Lerp(-halfWidth, halfWidth, i / (float)(totalCount - 1));
                        int lower = Mathf.FloorToInt(sample);
                        int upper = Mathf.Min(lower + 1, angleCount - 1);
                        offset = Mathf.Lerp(weaponData.homingLaunchAngles[lower],
                            weaponData.homingLaunchAngles[upper], sample - lower);
                    }
                    float radians = (baseAngle + offset) * Mathf.Deg2Rad;
                    SpawnProj(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)), default,
                        chargePercent, projectileDamageMultiplier, target, true);
                }
            }

            else if (totalCount <= 1)
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
            if (PlayStaffRecoil(strength))
            {
                return;
            }

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

        protected virtual Vector2 GetProjectileDirection(Vector2 direction) => direction;
        private void ApplyProjectileAnimation(GameObject projectile)
        {
            RuntimeAnimatorController[] variants = weaponData != null ? weaponData.projectileAnimationVariants : null;
            if (variants == null || variants.Length == 0) return;
            Animator projectileAnimator = projectile.GetComponentInChildren<Animator>();
            if (projectileAnimator == null) return;

            // 공격 묶음이 끝나도 순서를 유지하고 풀에서 재사용한 탄에도 다시 적용합니다.
            RuntimeAnimatorController selected = variants[nextProjectileAnimationIndex];
            nextProjectileAnimationIndex = (nextProjectileAnimationIndex + 1) % variants.Length;
            if (selected == null) return;
            projectileAnimator.runtimeAnimatorController = selected;
            projectileAnimator.Rebind();
            projectileAnimator.Update(0f);
        }

        protected virtual void ConfigureSpawnedProjectile(GameObject projectile, Vector2 direction, float chargePercent) { }
        protected virtual bool ShouldSpawnFireEffect() => true;
        protected virtual bool ShouldPlayFireAnimation() => true;
    }
}
