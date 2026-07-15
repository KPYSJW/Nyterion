using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Skills;
using UnityEngine;

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

        [Header("Ground Pet Settings")]
        [Tooltip("드론(지상 펫)의 이동 속도")]
        [SerializeField] private float followSpeed = 3.5f;
        [Tooltip("플레이어로부터 이탈 가능한 최대 거리 (이 거리를 넘으면 즉시 텔레포트 복귀)")]
        [SerializeField] private float leashRange = 5.5f;
        [Tooltip("공격 시 일시 정지하는 시간")]
        [SerializeField] private float attackFreezeDuration = 0.5f;

        [Header("Tail Follow Settings")]
        [Tooltip("플레이어 위치 기록 최소 간격 (미터)")]
        [SerializeField] private float waypointSpacing = 0.2f;
        [Tooltip("드론이 플레이어 뒤를 쫓을 때 유지하는 시간 차(웨이포인트 개수)")]
        [SerializeField] private int followDelayPoints = 8;

        [Header("Animation Settings")]
        [Tooltip("사망 애니메이션 재생 시간")]
        [SerializeField] private float deathDuration = 1.0f;
        [SerializeField] private string walkingBoolParam = "IsWalking";
        [SerializeField] private string attack1TriggerParam = "Attack1";
        [SerializeField] private string attack2TriggerParam = "Attack2";
        [SerializeField] private string deathTriggerParam = "Death";

        private Animator animator;
        private Vector3 lastPosition;
        private Vector3 lastPositionBeforeAttack;
        private Vector3 currentMoveTarget;
        private bool isAttack1Next = true;
        private Coroutine deathCoroutine;

        // 웨이포인트 꼬리 추적 변수들
        private Queue<Vector3> waypointQueue = new Queue<Vector3>();
        private Vector3 lastRecordedPlayerPos;

        private bool isActive = false;
        private float currentDurationTimer = 0f;
        private float autoAttackTimer = 0f;
        private float attackFreezeTimer = 0f;
        private static readonly Collider2D[] droneBuffer = new Collider2D[20];

        private void Start()
        {
            // 부모 자식 관계 해제 (플레이어 하위에서 독립된 월드 오브젝트로 분리하여 움직임 역전 현상 해결)
            transform.SetParent(null);

            if (droneVisual != null)
            {
                droneVisual.SetActive(false);
                animator = droneVisual.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = droneVisual.GetComponentInChildren<Animator>();
                }
            }
        }

        protected override void Activate()
        {
            if (deathCoroutine != null)
            {
                StopCoroutine(deathCoroutine);
                deathCoroutine = null;
            }

            // 이미 활성화된 상태면 지속 시간만 초기화
            if (isActive)
            {
                currentDurationTimer = 0f;
                return;
            }

            isActive = true;
            currentDurationTimer = 0f;
            autoAttackTimer = 0f; 
            attackFreezeTimer = 0f;

            // 초기 소환 위치 (시전자와 동일한 위치)
            transform.position = caster.position;
            lastPosition = transform.position;
            lastPositionBeforeAttack = transform.position;
            currentMoveTarget = transform.position;
            
            // 웨이포인트 초기화 (대기 상태의 지연을 위해 현재 플레이어 위치로 채움)
            waypointQueue.Clear();
            for (int i = 0; i < followDelayPoints; i++)
            {
                waypointQueue.Enqueue(caster.position);
            }
            lastRecordedPlayerPos = caster.position;

            if (droneVisual != null)
            {
                droneVisual.SetActive(true);
                if (animator != null)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
            }
        }

        private void Update()
        {
            // 시전자가 파괴된 경우 객체 소멸 (부모 해제 상태이므로 수동 소멸 처리)
            if (caster == null)
            {
                Destroy(gameObject);
                return;
            }

            // 비활성화 상태인 경우 로직 정지
            if (!isActive) return;

            // 타이머 갱신 및 소멸 확인
            currentDurationTimer += Time.deltaTime;
            if (currentDurationTimer >= activeDuration)
            {
                DeactivateDrone();
                return;
            }

            // 플레이어 이동 궤적 기록
            float distFromLastPoint = Vector3.Distance(caster.position, lastRecordedPlayerPos);
            if (distFromLastPoint > 12f)
            {
                // 플레이어가 텔레포트한 경우 즉시 이동 및 큐 초기화
                waypointQueue.Clear();
                transform.position = caster.position;
                for (int i = 0; i < followDelayPoints; i++)
                {
                    waypointQueue.Enqueue(caster.position);
                }
                lastRecordedPlayerPos = caster.position;
            }
            else if (distFromLastPoint >= waypointSpacing)
            {
                waypointQueue.Enqueue(caster.position);
                lastRecordedPlayerPos = caster.position;
                
                // 큐 최대 크기 제한
                while (waypointQueue.Count > followDelayPoints + 3)
                {
                    waypointQueue.Dequeue();
                }
            }

            // 공격 시 일시 정지 타이머 감소
            if (attackFreezeTimer > 0f)
            {
                attackFreezeTimer -= Time.deltaTime;
            }

            MoveDrone();

            // 이동 애니메이션 파라미터 업데이트 (X축 기준 목표치와의 수평 남은 거리를 비교)
            if (animator != null)
            {
                float horizontalDistance = Mathf.Abs(transform.position.x - currentMoveTarget.x);
                bool isMoving = (waypointQueue.Count > followDelayPoints && horizontalDistance > 0.15f);
                animator.SetBool(walkingBoolParam, isMoving);
            }
            lastPosition = transform.position;

            // 좌우 Flip 처리 (이동 방향 또는 공격 대상 조준 방향 반영)
            UpdateFlip();

            AutoAttackLogic();
        }

        /// <summary>
        /// 플레이어를 쫓아 지상형 펫처럼 지면을 매끄럽게 따라다니는 이동 구현 (공격 시 정지)
        /// </summary>
        private void MoveDrone()
        {
            if (attackFreezeTimer > 0f)
            {
                // 공격 중일 때는 이동을 멈춤
                transform.position = Vector3.Lerp(transform.position, lastPositionBeforeAttack, Time.deltaTime * 5f);
                return;
            }

            // 플레이어와의 실제 거리 계산
            float distanceToPlayer = Vector3.Distance(transform.position, caster.position);

            // 한계 거리를 초과하면 즉시 텔레포트 복귀
            if (distanceToPlayer > leashRange)
            {
                waypointQueue.Clear();
                transform.position = caster.position;
                for (int i = 0; i < followDelayPoints; i++)
                {
                    waypointQueue.Enqueue(caster.position);
                }
                lastRecordedPlayerPos = caster.position;
                return;
            }

            // 큐가 비어있지 않다면 가장 오래된 웨이포인트를 목표로 설정
            Vector3 targetPosition = caster.position;
            if (waypointQueue.Count > 0)
            {
                targetPosition = waypointQueue.Peek();
            }

            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

            // 큐 크기가 설정된 지연 포인트 수보다 크면 웨이포인트를 소비하며 이동
            if (waypointQueue.Count > followDelayPoints)
            {
                if (distanceToTarget < 0.2f)
                {
                    waypointQueue.Dequeue();
                    if (waypointQueue.Count > 0)
                    {
                        targetPosition = waypointQueue.Peek();
                    }
                }
            }

            currentMoveTarget = targetPosition;

            // Lerp를 사용하여 부드럽게 목표 위치로 이동 (Y축 bobbing이나 도약 없음)
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
        }

        /// <summary>
        /// 소환수의 이동 방향에 따라 좌우 스케일을 뒤집어 반전(Flip) 처리
        /// </summary>
        private void UpdateFlip()
        {
            // 공격 중에는 공격 동작 조준 방향이 우선이므로 뒤집기를 보류
            if (attackFreezeTimer > 0f) return;

            float deltaX = currentMoveTarget.x - transform.position.x;
            if (Mathf.Abs(deltaX) > 0.05f)
            {
                Vector3 scale = transform.localScale;
                if (deltaX > 0f)
                {
                    scale.x = Mathf.Abs(scale.x);
                }
                else
                {
                    scale.x = -Mathf.Abs(scale.x);
                }
                transform.localScale = scale;
            }
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
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, range, droneBuffer);
            Transform closestTarget = null;
            float closestDistanceSqr = Mathf.Infinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = droneBuffer[i];
                // 적 태그 및 데미지를 받을 수 있는 객체인지 확인
                if (hit.CompareTag("Enemy") && hit.GetComponent<IDamageable>() != null)
                {
                    float distanceSqr = (transform.position - hit.transform.position).sqrMagnitude;
                    if (distanceSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distanceSqr;
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
            // 공격을 가하기 시작하는 순간의 위치 고정 및 이동 정지 설정
            attackFreezeTimer = attackFreezeDuration;
            lastPositionBeforeAttack = transform.position;

            // 발사 방향 계산
            Vector2 direction = (target.position - transform.position).normalized;

            // 공격 조준 대상을 정면으로 바라보도록 회전(Flip)
            Vector3 scale = transform.localScale;
            if (direction.x > 0f)
            {
                scale.x = Mathf.Abs(scale.x);
            }
            else if (direction.x < 0f)
            {
                scale.x = -Mathf.Abs(scale.x);
            }
            transform.localScale = scale;

            GameObject projectile = ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, transform.position, Quaternion.identity);

            if (projectile != null)
            {
                // 날아가는 방향을 바라보도록 2D 회전 설정
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

                // 속도 적용
                if (projectile.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
                {
                    rb.velocity = direction * projectileSpeed;
                }

                // 드론이 발사하는 투사체는 기본적으로 관통 속성을 끄도록 예외 처리 
                if (projectile.TryGetComponent<Combat.PiercingEffect>(out Combat.PiercingEffect pierce))
                {
                    pierce.enabled = false;
                }
            }

            // 공격 애니메이션 재생
            if (animator != null)
            {
                if (isAttack1Next)
                {
                    animator.SetTrigger(attack1TriggerParam);
                }
                else
                {
                    animator.SetTrigger(attack2TriggerParam);
                }
                isAttack1Next = !isAttack1Next;
            }
        }

        private void DeactivateDrone()
        {
            if (!isActive) return;

            if (deathCoroutine != null)
            {
                StopCoroutine(deathCoroutine);
            }
            deathCoroutine = StartCoroutine(PlayDeathAndDeactivateRoutine());
        }

        private System.Collections.IEnumerator PlayDeathAndDeactivateRoutine()
        {
            isActive = false;

            if (animator != null)
            {
                animator.SetBool(walkingBoolParam, false);
                animator.SetTrigger(deathTriggerParam);
            }

            yield return new WaitForSeconds(deathDuration);

            if (droneVisual != null)
            {
                droneVisual.SetActive(false);
            }
            deathCoroutine = null;
        }
    }
}