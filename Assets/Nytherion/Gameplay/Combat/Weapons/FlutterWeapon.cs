using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class FlutterWeapon : ChargeableRangedWeapon
    {
        [Header("Flutter Charge Settings")]
        [SerializeField, Min(2)] private int minimumChargedArrowCount = 3;
        [SerializeField, Min(2)] private int maximumChargedArrowCount = 5;
        [SerializeField, Min(0f)] private float minimumSpreadAngle = 20f;
        [SerializeField, Min(0f)] private float maximumSpreadAngle = 45f;
        [SerializeField, Min(1f)] private float maximumDamageMultiplier = 2f;

        protected override void FireChargedAttack(Vector2 direction, float chargePercent)
        {
            bool isChargedAttack = IsChargingEnabled() && chargePercent > 0f;
            if (!isChargedAttack)
            {
                FireProjectiles(direction, 1);
                return;
            }

            float normalizedCharge = Mathf.Clamp01(chargePercent);
            int minArrowCount = Mathf.Max(2, minimumChargedArrowCount);
            int maxArrowCount = Mathf.Max(minArrowCount, maximumChargedArrowCount);
            int arrowCount = Mathf.RoundToInt(Mathf.Lerp(minArrowCount, maxArrowCount, normalizedCharge));
            float spreadAngle = Mathf.Lerp(minimumSpreadAngle, maximumSpreadAngle, normalizedCharge);
            float chargeDamageMultiplier = Mathf.Lerp(1f, maximumDamageMultiplier, normalizedCharge);

            FireProjectiles(
                direction,
                arrowCount,
                spreadAngle,
                normalizedCharge,
                chargeDamageMultiplier);
        }
    }
}
