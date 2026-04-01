using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public abstract class ChargeableRangedWeapon : RangedWeapon
    {
        [Header("Charge Settings")]
        public float maxChargeTime = 1.5f;

        protected float currentChargeTime = 0f;
        protected bool isCharging = false;

        private void Update()
        {
            // 마우스를 누르고 있는 동안 차징 시간 증가
            if (isCharging)
            {
                currentChargeTime += Time.deltaTime;
                currentChargeTime = Mathf.Clamp(currentChargeTime, 0f, maxChargeTime);

                float chargePercent = currentChargeTime / maxChargeTime;

                OnCharging(chargePercent);
            }
        }

        public override void Attack(Vector2 direction)
        {
            if (!CanAttack()) return;

            isCharging = true;
            currentChargeTime = 0f;
        }

        public override void AttackEnd()
        {
            if (!isCharging) return;

            isCharging = false;

            float finalChargePercent = currentChargeTime / maxChargeTime;

            Vector2 releaseDirection = transform.right;

            FireChargedAttack(releaseDirection, finalChargePercent);

            lastAttackTime = Time.time;
            currentChargeTime = 0f;
        }

        protected virtual void OnCharging(float chargePercent) { }

        protected abstract void FireChargedAttack(Vector2 direction, float chargePercent);
    }
}