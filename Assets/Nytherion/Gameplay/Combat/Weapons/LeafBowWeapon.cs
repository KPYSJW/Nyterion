using UnityEngine;
using System.Collections;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class LeafBowWeapon : ChargeableRangedWeapon
    {
        [Header("LeafBow Custom Settings")]
        [Tooltip("최대 차징 시 데미지 배율 (기본 데미지의 2배)")]
        [SerializeField] private float maxDamageMultiplier = 2.0f;

        [Tooltip("최대 차징 시 투사체 속도 배율 (기본 속도의 1.8배)")]
        [SerializeField] private float maxSpeedMultiplier = 1.8f;

        [Tooltip("관통 및 연두색 이펙트가 발동하는 최소 차징 임계값 (0.0 ~ 1.0)")]
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

            if (isRelicActive && chargePercent > 0f)
            {
                int extra = 0;
                if (playerManager != null && playerManager.currentPlayerData != null)
                {
                    extra = Mathf.FloorToInt(playerManager.currentPlayerData.extraProjectiles);
                }

                // 차징 정도(0.0 ~ 1.0)에 비례하여 최대 5개의 추가 탄환 개수를 조절
                int relicExtraCount = Mathf.RoundToInt(chargePercent * 5f);
                int totalCount = 1 + extra + relicExtraCount;

                if (totalCount > 1)
                {
                    StartCoroutine(FireLeafBowBurstRoutine(direction, totalCount, chargePercent));
                }
                else
                {
                    FireSingleChargedProjectile(direction, chargePercent);
                }
            }
            else
            {
                FireSingleChargedProjectile(direction, chargePercent);
            }

            // 발사 완료 후 무기 스케일을 원래대로 복원
            // transform.localScale = originalScale;
        }

        private IEnumerator FireLeafBowBurstRoutine(Vector2 direction, int totalCount, float chargePercent)
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

        private void FireSingleChargedProjectile(Vector2 direction, float chargePercent)
        {
            // 투사체 생성
            GameObject projObj = Projectile(direction);

            if (projObj != null)
            {
                // 1. 데미지 배율 적용 (차징 시간에 따라 50% ~ 200% 배율)
                CollisionObject collisionObj;
                if (projObj.TryGetComponent<CollisionObject>(out collisionObj))
                {
                    float currentDamageMultiplier = IsChargingEnabled() ? Mathf.Lerp(0.5f, maxDamageMultiplier, chargePercent) : 1.0f;
                    collisionObj.damage = weaponData.damage * currentDamageMultiplier;
                }

                // 2. 투사체 속도 배율 적용 (차징 시간에 따라 50% ~ 180% 속도)
                float speedMultiplier = IsChargingEnabled() ? Mathf.Lerp(0.5f, maxSpeedMultiplier, chargePercent) : 1.0f;
                float finalSpeed = weaponData.projectileSpeed * speedMultiplier;

                Rigidbody2D rb;
                if (projObj.TryGetComponent<Rigidbody2D>(out rb))
                {
                    rb.velocity = direction.normalized * finalSpeed;
                }

                IProjectile iProj;
                if (projObj.TryGetComponent<IProjectile>(out iProj))
                {
                    iProj.SetSpeed(finalSpeed);
                }

                // 3. 풀 차징 추가 혜택 (관통 활성화, 크기 확대, 연두색 강화)
                PiercingEffect piercingEffect = projObj.GetComponent<PiercingEffect>();
                if (piercingEffect == null) return;

                if (IsChargingEnabled() && chargePercent >= pierceThreshold)
                {
                    piercingEffect.enabled = true;

                    // 화살 비주얼 확대 (1.4배)
                    projObj.transform.localScale *= 1.4f;

                    // 화살 스프라이트 색상을 연두색(Forest/Lime Green)으로 변경
                    SpriteRenderer sr;
                    if (projObj.TryGetComponent<SpriteRenderer>(out sr))
                    {
                        sr.color = new Color(0.35f, 1.0f, 0.15f, 1.0f);
                    }
                }
                else
                {
                    piercingEffect.enabled = false;
                }
            }
        }
    }
}
