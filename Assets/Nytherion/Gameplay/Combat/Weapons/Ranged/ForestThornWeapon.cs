using UnityEngine;
using System.Collections;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class ForestThornWeapon : ChargeableRangedWeapon
    {
        [Header("Forest Thorn Custom Settings")]
        [Tooltip("최대 차징 시 데미지 배율 (기본 데미지의 2배)")]
        [SerializeField] private float maxDamageMultiplier = 2.0f;

        [Tooltip("최대 차징 시 투사체 속도 배율 (기본 속도의 1.8배)")]
        [SerializeField] private float maxSpeedMultiplier = 1.8f;

        [Tooltip("관통 및 크기 확대가 발동하는 최소 차징 임계값 (0.0 ~ 1.0)")]
        [SerializeField] private float pierceThreshold = 0.9f;

        private Vector3 originalScale;

        private void Start()
        {
            originalScale = transform.localScale;
        }

        protected override void OnCharging(float chargePercent)
        {
            // 차징 중에 활의 크기를 점차 키워 당기는 힘을 표현 (1.0배 -> 1.25배)
            // float scaleMultiplier = Mathf.Lerp(1.0f, 1.25f, chargePercent);
            // transform.localScale = originalScale * scaleMultiplier;
        }

        protected override void FireChargedAttack(Vector2 direction, float chargePercent)
        {
            // "유물에 의해 차징이 가능해진 경우" 조건 체크
            // 1. weaponData.requiredRelicId가 존재하고
            // 2. RelicManager에서 해당 유물이 활성화되어 있고
            // 3. 실제로 차징을 시도해서 쏜 경우 (chargePercent > 0f)
            bool isRelicActive = false;
            if (weaponData != null && !string.IsNullOrEmpty(weaponData.requiredRelicId) &&
                playerManager != null && playerManager.playerRelicManager != null)
            {
                isRelicActive = playerManager.playerRelicManager.IsRelicActive(weaponData.requiredRelicId);
            }

            int extra = playerManager != null && playerManager.currentPlayerData != null
                ? Mathf.Max(0, Mathf.FloorToInt(playerManager.currentPlayerData.extraProjectiles)) : 0;

            // 차징 정도(0.0 ~ 1.0)에 비례하여 최대 5개의 추가 탄환 개수를 조절
            int relicExtraCount = isRelicActive && chargePercent > 0f ? Mathf.RoundToInt(chargePercent * 5f) : 0;
            int totalCount = 1 + extra + relicExtraCount;

            if (totalCount > 1)
            {
                StartCoroutine(FireForestThornBurstRoutine(direction, totalCount, chargePercent));
            }
            else
            {
                FireSingleChargedProjectile(direction, chargePercent);
            }

            // 연사 탄환별이 아니라 공격 묶음당 한 번 알리고, 차징 피해 배율도 함께 전달합니다.
            EventManager eventManager = playerManager != null ? playerManager.EventManager : null;
            if (eventManager != null && weaponData != null)
            {
                float chargeDamageMultiplier = IsChargingEnabled()
                    ? Mathf.Lerp(0.5f, maxDamageMultiplier, chargePercent)
                    : 1f;
                float baseDamage = weaponData.damage * EffectiveDamageMultiplier * chargeDamageMultiplier;
                eventManager.TriggerPlayerRangedAttack(direction, totalCount, baseDamage, firePoint, projectilePoolTag);
            }

            // 발사 완료 후 무기 스케일을 원래대로 복원
            // transform.localScale = originalScale;
        }

        private IEnumerator FireForestThornBurstRoutine(Vector2 direction, int totalCount, float chargePercent)
        {
            WaitForSeconds wait = new WaitForSeconds(burstInterval);
            for (int i = 0; i < totalCount; i++)
            {
                FireSingleChargedProjectile(direction, chargePercent);
                if (i < totalCount - 1)
                {
                    yield return wait;
                }
            }
        }

        protected override bool ShouldSpawnFireEffect()
        {
            // 공통 공격 묶음 대신 실제 화살 생성 시점마다 발사 연출을 재생합니다.
            return false;
        }

        protected override void ConfigureSpawnedProjectile(GameObject projectile, Vector2 direction, float chargePercent)
        {
            base.ConfigureSpawnedProjectile(projectile, direction, chargePercent);
            if (firePoint == null || weaponData == null || weaponData.fireEffectPrefab == null) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            WeaponVFXHelper.PlayFireEffect(weaponData.fireEffectPrefab, firePoint.position,
                Quaternion.AngleAxis(angle, Vector3.forward), firePoint);
        }

        private void FireSingleChargedProjectile(Vector2 direction, float chargePercent)
        {
            // 투사체 생성
            GameObject projObj = SpawnProj(direction, default, chargePercent);

            float recoilStrength = IsChargingEnabled()
                ? Mathf.Lerp(0.7f, 1.3f, chargePercent)
                : 1f;
            PlayWeaponRecoil(direction, recoilStrength);

            if (projObj != null)
            {
                // 1. 데미지 배율 적용 (차징 시간에 따라 50% ~ 200% 배율)
                CollisionObject collisionObj;
                if (projObj.TryGetComponent<CollisionObject>(out collisionObj))
                {
                    float currentDamageMultiplier = IsChargingEnabled() ? Mathf.Lerp(0.5f, maxDamageMultiplier, chargePercent) : 1.0f;
                    collisionObj.damage *= currentDamageMultiplier;
                }

                // 2. 투사체 속도 배율 적용 (차징 시간에 따라 50% ~ 180% 속도)
                float speedMultiplier = IsChargingEnabled() ? Mathf.Lerp(0.5f, maxSpeedMultiplier, chargePercent) : 1.0f;
                float finalSpeed = weaponData.projectileSpeed * speedMultiplier;

                Rigidbody2D rb;
                if (projObj.TryGetComponent<Rigidbody2D>(out rb))
                {
                    rb.velocity = direction.normalized * finalSpeed;
                }

                IProj iProj;
                if (projObj.TryGetComponent<IProj>(out iProj))
                {
                    iProj.SetSpeed(finalSpeed);
                }

                // 3. 풀 차징 추가 혜택 (관통 활성화, 크기 확대)
                PiercingModifier piercingModifier = projObj.GetComponent<PiercingModifier>();
                if (piercingModifier == null) return;

                if (IsChargingEnabled() && chargePercent >= pierceThreshold)
                {
                    piercingModifier.enabled = true;

                    // 화살 비주얼 확대 (1.4배)
                    projObj.transform.localScale *= 1.4f;
                }
            }
        }
    }
}
