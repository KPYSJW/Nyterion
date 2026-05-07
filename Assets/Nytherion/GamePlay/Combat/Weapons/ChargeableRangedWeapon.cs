using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public abstract class ChargeableRangedWeapon : RangedWeapon
    {
        [Header("Charge Settings")]
        public float maxChargeTime = 1.5f;

        protected float currentChargeTime = 0f;
        protected bool isCharging = false;

        protected float GetAdjustedMaxChargeTime()
        {
            if (playerManager == null || playerManager.currentPlayerData == null)
            {
                return maxChargeTime;
            }

            // 차징 시간 감소율 적용 (1.0 = 100% 감소 = 0초)
            float reduction = playerManager.currentPlayerData.chargeTimeReduction;
            return Mathf.Max(0f, maxChargeTime * (1f - reduction));
        }

        private void Update()
        {
            if (isCharging)
            {
                float adjustedMaxChargeTime = GetAdjustedMaxChargeTime();

                if (adjustedMaxChargeTime <= 0f)
                {
                    // 차징 시간이 0이면 즉시 최대 차징으로 발사
                    FireImmediate();
                    return;
                }

                currentChargeTime += Time.deltaTime;
                currentChargeTime = Mathf.Clamp(currentChargeTime, 0f, adjustedMaxChargeTime);

                float chargePercent = currentChargeTime / adjustedMaxChargeTime;

                OnCharging(chargePercent);
            }
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            float adjustedMaxChargeTime = GetAdjustedMaxChargeTime();
            if (adjustedMaxChargeTime <= 0f)
            {
                FireImmediate();
                return;
            }

            isCharging = true;
            currentChargeTime = 0f;
        }

        private void FireImmediate()
        {
            isCharging = false;
            currentChargeTime = 0f;

            Vector2 releaseDirection = transform.right;
            FireChargedAttack(releaseDirection, 1.0f); // 1.0 = 풀차징

            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
            if (!isCharging) return;

            isCharging = false;

            float adjustedMaxChargeTime = GetAdjustedMaxChargeTime();
            float finalChargePercent = adjustedMaxChargeTime > 0f ? (currentChargeTime / adjustedMaxChargeTime) : 1.0f;

            Vector2 releaseDirection = transform.right;

            FireChargedAttack(releaseDirection, finalChargePercent);

            lastAttackTime = Time.time;
            currentChargeTime = 0f;
        }

        protected virtual void OnCharging(float chargePercent) { }

        protected abstract void FireChargedAttack(Vector2 direction, float chargePercent);
    }
}