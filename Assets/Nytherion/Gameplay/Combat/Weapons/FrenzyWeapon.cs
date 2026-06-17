using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class FrenzyWeapon : RangedWeapon
    {
        [Header("Frenzy Spin-up Settings")]
        [Tooltip("스핀업 완료에 걸리는 시간")]
        [SerializeField] private float spinUpTime = 1.0f;

        [Tooltip("발사 이펙트 애니메이션 시작 속도")]
        [SerializeField] private float minAnimSpeed = 0.5f;

        [Tooltip("발사 이펙트 애니메이션 최고 속도")]
        [SerializeField] private float maxAnimSpeed = 3.0f;

        [Tooltip("투사체 발사를 시작하기 위한 스핀업 진행도 임계값 (0.0 ~ 1.0)")]
        [SerializeField] private float fireThreshold = 0.9f;

        private bool isAttacking = false;
        private float currentSpinUpProgress = 0f;
        private GameObject activeFireEffectInstance = null;
        private float lastProjectileFireTime = 0f;
        private Vector2 lastAimDirection = Vector2.right;

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            lastAimDirection = direction;

            if (!isAttacking)
            {
                isAttacking = true;
                currentSpinUpProgress = 0f;
                lastProjectileFireTime = 0f;

                // 스핀업 시작 시 이펙트를 firePoint의 자식으로 등록하여 생성
                if (firePoint != null && weaponData != null && weaponData.fireEffectPrefab != null)
                {
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);

                    if (ObjectPoolManager.Instance != null)
                    {
                        activeFireEffectInstance = ObjectPoolManager.Instance.SpawnFromPool(weaponData.fireEffectPrefab, firePoint.position, rotation);
                    }
                    else
                    {
                        activeFireEffectInstance = Instantiate(weaponData.fireEffectPrefab, firePoint.position, rotation);
                    }

                    if (activeFireEffectInstance != null)
                    {
                        activeFireEffectInstance.transform.SetParent(firePoint);

                        // 차징 및 연속 발사 도중 이펙트가 임의로 소멸되는 것을 방지하기 위해 자동반환 비활성화
                        AutoReturnToPool autoReturn;
                        if (activeFireEffectInstance.TryGetComponent<AutoReturnToPool>(out autoReturn))
                        {
                            autoReturn.enabled = false;
                        }

                        // 이펙트 애니메이터 속도 초기화
                        Animator animator;
                        if (activeFireEffectInstance.TryGetComponent<Animator>(out animator))
                        {
                            animator.speed = minAnimSpeed;
                        }
                    }
                }
            }
        }

        public override void AttackEnd()
        {
            ResetFrenzyState();
        }

        private void Update()
        {
            if (isAttacking)
            {
                // 플레이어가 조준하는 마우스 방향에 따라 지속형 머즐 플래시 회전값 갱신
                if (activeFireEffectInstance != null && firePoint != null)
                {
                    float angle = Mathf.Atan2(lastAimDirection.y, lastAimDirection.x) * Mathf.Rad2Deg;
                    activeFireEffectInstance.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                }

                // 스핀업 진행도 가속
                if (currentSpinUpProgress < 1f)
                {
                    currentSpinUpProgress += Time.deltaTime / spinUpTime;
                    currentSpinUpProgress = Mathf.Clamp01(currentSpinUpProgress);

                    // 진행도 보간값에 맞춰 이펙트 애니메이션 속도 제어
                    if (activeFireEffectInstance != null)
                    {
                        Animator animator;
                        if (activeFireEffectInstance.TryGetComponent<Animator>(out animator))
                        {
                            animator.speed = Mathf.Lerp(minAnimSpeed, maxAnimSpeed, currentSpinUpProgress);
                        }
                    }
                }

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
            // 연사 과정에서 지속형 이펙트를 쓰고 있으므로 개별 투사체 스폰 시의 1회성 이펙트 생성 방지
            return false;
        }

        protected override bool ShouldPlayFireAnimation()
        {
            // 매 발사 시 캐릭터/무기 애니메이션 트리거가 튀는 것 방지
            return false;
        }

        private void ResetFrenzyState()
        {
            isAttacking = false;
            currentSpinUpProgress = 0f;

            if (activeFireEffectInstance != null)
            {
                // AutoReturnToPool 컴포넌트를 복구하여 안전하게 풀 반환되도록 초기화
                AutoReturnToPool autoReturn;
                if (activeFireEffectInstance.TryGetComponent<AutoReturnToPool>(out autoReturn))
                {
                    autoReturn.enabled = true;
                }

                if (ObjectPoolManager.Instance != null && weaponData != null && weaponData.fireEffectPrefab != null)
                {
                    ObjectPoolManager.Instance.ReturnToPool(weaponData.fireEffectPrefab.name, activeFireEffectInstance);
                }
                else
                {
                    Destroy(activeFireEffectInstance);
                }
                activeFireEffectInstance = null;
            }
        }

        private void OnDisable()
        {
            ResetFrenzyState();
        }
    }
}
