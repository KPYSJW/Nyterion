using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Skills;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Characters.Skill
{
    /// <summary>
    /// 활성화 시 플레리어 주변을 공전하며 일정 반경 내의 적을 찾아 자동으로 투사체를 발사하는 드론을 소환하는 스킬
    /// </summary>
    public class DroneSkill : SkillBase
    {
        [Header("Drone Visuals")]
        public GameObject droneVisual;

        [Header("Drone Settings")]
        [Tooltip("드론 유지 시간")]
        public float activeDuration = 10f;
        [Tooltip("드론 투사체 발사 간격")]
        public float attackInterval = 1f;
        [Tooltip("ObjectPoolManager에 등록된 투사체 태그")]
        public string projectilePoolTag = "Arrow";
        [Tooltip("투사체 발사 속도")]
        public float projectileSpeed = 10f;

        [Header("Orbit Settings")]
        [Tooltip("시전자로부터의 공전 반경")]
        public float orbitRadius = 1.5f;
        [Tooltip("공전 회전 속도 (도/초)")]
        public float orbitSpeed = 90f;
        [Tooltip("상하로 둥둥 떠다니는 진동 폭")]
        public float bobbingAmount = 0.2f;
        [Tooltip("상하 진동 속도")]
        public float bobbingSpeed = 3f;
        private bool isActive = false;
        private float currentDurationTimer = 0f;
        private float autoAttackTimer = 0f;
        private float currentOrbitAngle = 0f;

        private void Start()
        {
            if (droneVisual != null) droneVisual.SetActive(false);
        }

        protected override void Activate()
        {
            // 이미 활성화된 상태면 지속 시간만 초기화
            if (isActive)
            {
                currentDurationTimer = 0f;
                return;
            }

            isActive = true;
            currentDurationTimer = 0f;
            autoAttackTimer = 0f; 
            currentOrbitAngle = 0f;

            // 초기 소환 위치 (시전다의 우측)
            transform.position = caster.position + new Vector3(orbitRadius, 0, 0);

            if (droneVisual != null) droneVisual.SetActive(true);
        }

        private void Update()
        {
            // 비활성화 상태거나 시전자가 파괴된 경우 로직 정지
            if (!isActive || caster == null) return;

            // 타이머 갱신 및 소멸 확인
            currentDurationTimer += Time.deltaTime;
            if (currentDurationTimer >= activeDuration)
            {
                DeactivateDrone();
                return;
            }

            MoveDrone();

            AutoAttackLogic();
        }

        /// <summary>
        /// 시전자를 중심으로 드론이 궤도를 돌며 둥둥 떠다니는 애니메잇견 효과를 구현
        /// </summary>
        private void MoveDrone()
        {
            currentOrbitAngle += Time.deltaTime * orbitSpeed;
            float rad = currentOrbitAngle * Mathf.Deg2Rad;

            // sin 그래프를 이용하여 위아래로 자연스럽게 움직이는 효과 추가
            float bobbingOffset = Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;

            // 원의 방정식을 이용하여 시전자를 중심으로 공전하는 위치 계산
            Vector3 targetPosition = caster.position + new Vector3(Mathf.Cos(rad) * orbitRadius, Mathf.Sin(rad) * orbitRadius + bobbingOffset, 0);

            // Lerp를 사용하여 부드럽게 목표 위치로 이동
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);
        }

        /// <summary>
        /// 쿨타임마다 범위 내 적을 탐색하여 투사체를 발사하는 로직
        /// </summary>
        private void AutoAttackLogic()
        {
            if (skillData == null) return;

            autoAttackTimer += Time.deltaTime;
            if (autoAttackTimer >= attackInterval)
            {
                Transform target = FindClosestEnemy();
                if (target != null)
                {
                    FireAtTarget(target);
                    autoAttackTimer = 0f;
                }
            }
        }

        /// <summary>
        /// 스킬 사거리 내의 모든 적을 검색하고 가장 가까운 적을 반환
        /// </summary>
        /// <returns></returns>
        private Transform FindClosestEnemy()
        {
            float range = skillData.range;

            // 지정 반경 내의 모든 2D 콜라이더 검색
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
            Transform closestTarget = null;
            float closestDistance = Mathf.Infinity;

            foreach (var hit in hits)
            {
                // 적 태그 및 데미지를 받을 수 있는 객체인지 확인
                if (hit.CompareTag("Enemy") && hit.GetComponent<IDamageable>() != null)
                {
                    float distance = Vector2.Distance(transform.position, hit.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = hit.transform;
                    }
                }
            }
            return closestTarget;
        }

        /// <summary>
        /// 타겟을 향해 지정된 투사체를 오브젝트 풀에서 꺼내어 발사
        /// </summary>
        /// <param name="target"></param>
        private void FireAtTarget(Transform target)
        {
            // 발사 방향 계산
            Vector2 direction = (target.position - transform.position).normalized;

            GameObject projectile = ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, transform.position, Quaternion.identity);

            if (projectile != null)
            {
                // 날아가는 방향을 바라보도록 2D 회전 설정
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

                // 속도 적용
                if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.velocity = direction * projectileSpeed;
                }

                // 드론이 발사하는 투사체는 기본적으로 관통 속성을 끄도록 예외 처리 
                if (projectile.TryGetComponent<Combat.PiercingEffect>(out var pierce))
                {
                    pierce.enabled = false;
                }
            }
        }

        private void DeactivateDrone()
        {
            isActive = false;
            if (droneVisual != null) droneVisual.SetActive(false);
        }
    }
}