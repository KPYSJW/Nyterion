using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class ChargedPiercingWeapon : ChargeableRangedWeapon
    {
        [Header("Piercing & Damage Settings")]
        [Tooltip("최대 차징 시 곱해질 데미지 배율")]
        public float maxDamageMultiplier = 2.0f;

        [Tooltip("관통 효과가 발동하기 위한 최소 차징 비율 (0.0 ~ 1.0)")]
        public float pierceThreshold = 0.8f;

        private Vector3 originalScale;

        private void Start()
        {
            originalScale = transform.localScale;
        }

        protected override void FireChargedAttack(Vector2 direction, float chargePercent)
        {
            // RangedWeapon의 Projectile을 호출하여 투사체를 생성
            GameObject projObj = Projectile(direction);

            // 투사체에 붙어있는 CollisionObject를 찾아 값을 덮어씌운다.
            if (projObj != null && projObj.TryGetComponent<CollisionObject>(out var collisionObj))
            {
                // 기본 데미지에서 차징 비율만큼 배율을 적용
                float currentDamageMultiplier = Mathf.Lerp(1.0f, maxDamageMultiplier, chargePercent);
                collisionObj.damage = weaponData.damage * currentDamageMultiplier;

                // 차징 비율이 임계치를 넘으면 관통 효과를 활성화
                if (chargePercent >= pierceThreshold)
                {
                    collisionObj.isPiercing = true;

                    // 시각적 피드백: 풀 차징 관통 투사체는 크기를 1.5배 키우고 빨간색으로 변경
                    projObj.transform.localScale *= 1.5f;
                    if (projObj.TryGetComponent<SpriteRenderer>(out var sr))
                    {
                        sr.color = Color.red;
                    }
                }
                else
                {
                    collisionObj.isPiercing = false;
                }
            }
            transform.localScale = originalScale;
        }

        protected override void OnCharging(float chargePercent)
        {
            float scaleMultiplier = Mathf.Lerp(1.0f, 1.3f, chargePercent);
            transform.localScale = originalScale * scaleMultiplier;
        }
    }
}