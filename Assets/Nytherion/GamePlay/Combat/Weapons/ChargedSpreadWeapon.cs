using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class ChargedSpreadWeapon : ChargeableRangedWeapon
    {
        [Header("Charged Spread Settings")]
        [Tooltip("�ִ� ��¡ �� �߻�� ����ü ����")]
        public int maxProjectileCount = 5;
        [Tooltip("��ä�� �߻� ����")]
        public float spreadAngle = 45f;
        private Vector3 originalScale;

        private void Start()
        {
            originalScale = transform.localScale;
        }
        protected override void FireChargedAttack(Vector2 direction, float chargePercent)
        {
            int currentProjectileCount = Mathf.FloorToInt(Mathf.Lerp(1, maxProjectileCount, chargePercent));

            FireProjectiles(direction, currentProjectileCount, spreadAngle);

            transform.localScale = originalScale;
        }

        protected override void OnCharging(float chargePercent)
        {
            float scaleMultiplier = Mathf.Lerp(1.0f, 1.3f, chargePercent);

            transform.localScale = originalScale * scaleMultiplier;
        }
    }
}