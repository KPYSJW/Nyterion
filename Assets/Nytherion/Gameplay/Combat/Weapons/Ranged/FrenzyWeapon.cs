using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class FrenzyWeapon : RangedWeapon, IChargeableWeapon
    {
        [Header("Frenzy Spin-up Settings")]
        [Tooltip("스핀업 완료에 걸리는 시간")]
        [SerializeField] private float spinUpTime = 1.0f;

        [Tooltip("투사체 발사를 시작하기 위한 스핀업 진행도 임계값 (0.0 ~ 1.0)")]
        [SerializeField] private float fireThreshold = 0.9f;

        [Header("Frenzy 탄착군 설정")]
        [Tooltip("조준 방향에서 탄환이 좌우로 벗어날 수 있는 최대 각도. 0이면 정확히 직진합니다.")]
        [SerializeField, Min(0f)] private float spreadHalfAngle = 4f;

        private bool isAttacking = false;
        private float currentSpinUpProgress = 0f;
        private float lastProjectileFireTime = 0f;
        private Vector2 lastAimDirection = Vector2.right;

        public bool IsCharging => isAttacking && HasChargeRelic();
        public float ChargePercent => currentSpinUpProgress;

        protected override Vector2 GetProjectileDirection(Vector2 direction)
        {
            // 추가 연사 탄환에도 매번 별도의 편차를 적용한다.
            float halfAngle = Mathf.Max(0f, spreadHalfAngle);
            if (halfAngle > 0f)
            {
                float angleOffset = Random.Range(-halfAngle, halfAngle);
                direction = Quaternion.AngleAxis(angleOffset, Vector3.forward) * direction;
            }

            return direction;
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (firePoint == null || weaponData == null) return;
            lastAimDirection = direction;

            if (!isAttacking)
            {
                isAttacking = true;
                currentSpinUpProgress = 0f;
                lastProjectileFireTime = 0f;
            }
        }

        public override void AttackEnd()
        {
            ResetFrenzyState();
        }

        private void Update()
        {
            if (isAttacking && firePoint != null && weaponData != null)
            {
                lastAimDirection = firePoint.right;

                // 스핀업 진행도 가속
                if (currentSpinUpProgress < 1f)
                {
                    currentSpinUpProgress += Time.deltaTime / Mathf.Max(0.01f, spinUpTime);
                    currentSpinUpProgress = Mathf.Clamp01(currentSpinUpProgress);
                }

                damageMultiplier = HasChargeRelic()
                    ? Mathf.Lerp(1f, 2f, currentSpinUpProgress)
                    : 1f;

                // 스핀업 임계치 도달 후 연사 쿨다운마다 투사체 발사
                if (currentSpinUpProgress >= fireThreshold)
                {
                    if (Time.time - lastProjectileFireTime >= weaponData.cooldown)
                    {
                        FireProjectiles(lastAimDirection, 1);
                        lastProjectileFireTime = Time.time;
                        lastAttackTime = Time.time;
                    }
                }
            }
        }

        public override bool CanAttack()
        {
            // 차징 중에도 조준 방향 업데이트를 매 프레임 호출받기 위해 항상 true 반환
            return true;
        }

        protected override bool ShouldSpawnFireEffect()
        {
            // 발사 묶음이 아닌 실제 탄환 스폰 시점에 재생하여 추가 연사 탄에도 맞춥니다.
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

        protected override bool ShouldPlayFireAnimation()
        {
            // 매 발사 시 캐릭터/무기 애니메이션 트리거가 튀는 것 방지
            return false;
        }

        private bool HasChargeRelic()
        {
            return playerManager != null &&
                   playerManager.playerRelicManager != null &&
                   playerManager.playerRelicManager.IsRelicActive("ChargeRelic");
        }

        private void ResetFrenzyState()
        {
            isAttacking = false;
            currentSpinUpProgress = 0f;
            damageMultiplier = 1f;
            // 버튼을 놓거나 무기를 교체하면 아직 대기 중인 추가 연사도 중단합니다.
            StopAllCoroutines();
        }

        private void OnDisable()
        {
            ResetFrenzyState();
        }
    }
}
