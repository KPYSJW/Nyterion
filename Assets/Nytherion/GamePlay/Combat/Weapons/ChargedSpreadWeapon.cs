using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class ChargedSpreadWeapon : ChargeableRangedWeapon
    {
        [Header("Charged Spread Settings")]
        [Tooltip("최대 투사체 수")]
        public int maxProjectileCount = 5;
        public float spreadAngle = 45f;
        private Vector3 originalScale;

        private void Start()
        {
            originalScale = transform.localScale;
        }
        protected override void FireChargedAttack(Vector2 direction, float chargePercent)
        {
            int currentProjectileCount = IsChargingEnabled() ? Mathf.FloorToInt(Mathf.Lerp(1, maxProjectileCount, chargePercent)) : 1;

            FireProjectiles(direction, currentProjectileCount, spreadAngle, chargePercent);

            transform.localScale = originalScale;
        }

        protected override void OnCharging(float chargePercent)
        {
            float scaleMultiplier = Mathf.Lerp(1.0f, 1.3f, chargePercent);

            transform.localScale = originalScale * scaleMultiplier;
        }
    }
}