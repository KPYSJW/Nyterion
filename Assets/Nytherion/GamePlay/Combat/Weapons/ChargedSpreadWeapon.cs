using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class ChargedSpreadWeapon : ChargeableRangedWeapon
    {
        [Header("Charged Spread Settings")]
        [Tooltip("최대 차징 시 발사될 투사체 개수")]
        public int maxProjectileCount = 5;
        [Tooltip("부채꼴 발사 각도")]
        public float spreadAngle = 45f;
        private Vector3 originalScale;

        private void Start()
        {
            originalScale = transform.localScale;
        }
        protected override void FireChargedAttack(Vector2 direction, float chargePercent)
        {
            int currentProjectileCount = Mathf.FloorToInt(Mathf.Lerp(1, maxProjectileCount, chargePercent));

            if (currentProjectileCount == 1)
            {
                Projectile(direction);
            }
            else
            {
                float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                float startAngle = baseAngle - (spreadAngle / 2f);
                float angleStep = spreadAngle / (currentProjectileCount - 1);

                for (int i = 0; i < currentProjectileCount; i++)
                {
                    float currentAngle = startAngle + (angleStep * i);
                    Vector2 spreadDirection = new Vector2(
                        Mathf.Cos(currentAngle * Mathf.Deg2Rad),
                        Mathf.Sin(currentAngle * Mathf.Deg2Rad)
                    );
                    Projectile(spreadDirection);
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