using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Skills;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Skill
{
    /// <summary>
    /// 활성화 시 플레이어 주변을 따라다니며 3순위 전투 어그로 타겟팅과 카이팅 회피 기동을 수행하는 슬라임 소환 스킬
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
        [Tooltip("플레이어로부터 이탈 가능한 최대 거리 (이 거리를 넘으면 즉시 텔레포트 복귀)")]
        [SerializeField] private float leashRange = 12.0f;
        [Tooltip("공격 시 일시 정지하는 시간")]
        [SerializeField] private float attackFreezeDuration = 0.5f;

        [Header("Dead Zone Settings")]
        [Tooltip("소환수가 움직이기 시작하는 플레이어와의 최소 거리 (소환수 중심의 데드존 반경)")]
        [SerializeField] private float wakeupDistance = 2.5f;
        [Tooltip("추적을 중지하는 안착 반경")]
        [SerializeField] private float stopRadius = 0.15f;

        [Header("Speed & Acceleration Settings")]
        [Tooltip("추적 기동 시 최소 제한 속도 (기동 출발 및 감속 시 하한선)")]
        [SerializeField] private float minMoveSpeed = 1.5f;
        [Tooltip("추적 기동 시 최대 제한 속도 (가속 시 상한선)")]
        [SerializeField] private float maxMoveSpeed = 5.5f;
        [Tooltip("기동 가속도 (m/s^2)")]
        [SerializeField] private float acceleration = 12.0f;

        [Header("Delay Settings")]
        [Tooltip("플레이어가 데드존에 들어온 후 소환수가 정지하기까지 추가로 더 이동하는 지연 시간 (초)")]
        [SerializeField] private float stopDelayDuration = 0.6f;


        [Header("Teleport Settings")]
        [Tooltip("텔레포트 시 재생할 이펙트 프리팹 (선택사항)")]
        [SerializeField] private GameObject teleportEffectPrefab;
        [Tooltip("오브젝트 풀에서 가져올 텔레포트 이펙트 태그 (선택사항)")]
        [SerializeField] private string teleportEffectPoolTag = "";

        [Header("Animation Settings")]
        [Tooltip("걷기 애니메이션 제어용 bool 파라미터 이름")]
        [SerializeField] private string walkingBoolParam = "IsWalking";
        [SerializeField] private string attack1TriggerParam = "Attack1";
        [SerializeField] private string attack2TriggerParam = "Attack2";
        [SerializeField] private string deathTriggerParam = "Death";

        private Animator animator;
        private Vector3 lastPosition;
        private Vector3 lastRecordedPlayerPos;
        private bool isAttack1Next = true;


        // 플레이어 최근 공격 및 피격 타겟 정보 (일점사 및 보복 구현용)
        private PlayerCombat playerCombat;
        private Vector3 lastPlayerAttackPos;
        private float lastPlayerAttackTime = -999f;

        private Transform defensiveTarget;
        private float lastPlayerHurtTime = -999f;
        private float lastRecordedPlayerHealth = -1f;


        private bool isFollowMoving = false;

        private bool isActive = false;
        private float currentDurationTimer = 0f;
        private float autoAttackTimer = 0f;
        private float attackFreezeTimer = 0f;
        private static readonly Collider2D[] droneBuffer = new Collider2D[20];

        // 엔터 더 건전 스타일 물리 컴패니언 제어 변수들
        private Rigidbody2D rb;
        private Vector3 currentShoulderOffset;
        private Vector3 targetShoulderOffset;
        private Vector3 currentTargetPosition;
        private bool isMovingToTarget = false;
        private float currentSpeed = 0f;

        // 정지 지연(슬라이딩) 상태 제어 변수들
        private bool isStoppingDelayActive = false;
        private float stopDelayTimer = 0f;
        private Vector3 stopDirection = Vector3.zero;
        private float stopSpeed = 0f;

        private void Start()
        {
            // 부모 자식 관계 해제 (플레이어 하위에서 독립된 월드 오브젝트로 분리하여 움직임 역전 현상 해결)
            transform.SetParent(null);

            EnsureRigidbodyInitialized();

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

        private void EnsureRigidbodyInitialized()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
                if (rb == null)
                {
                    rb = gameObject.AddComponent<Rigidbody2D>();
                }
            }
            rb.gravityScale = 0f;
            rb.drag = 3.5f; // 유니티 내장 물리 댐퍼(Damper) 기능 수행
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 렌더 프레임과 물리 틱 비동기 떨림 방지
        }

        protected override void Activate()
        {
            // 부모 자식 관계 즉시 해제 (Activate가 Start보다 먼저 실행될 경우 대비)
            transform.SetParent(null);

            EnsureRigidbodyInitialized();

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

            // 가상 어깨 오프셋 초기화 (시선 좌우 방향 감지)
            float initialSide = GetTargetSide();
            targetShoulderOffset = new Vector3(initialSide * 1.2f, 0.6f, 0f);
            currentShoulderOffset = targetShoulderOffset;

            // 초기 소환 위치 (플레이어 본체 위치에 스폰)
            transform.position = caster.position;
            rb.position = transform.position;
            rb.velocity = Vector2.zero;

            lastPosition = transform.position;

            isFollowMoving = false;
            isMovingToTarget = false;
            currentSpeed = 0f;
            isStoppingDelayActive = false;
            stopDelayTimer = 0f;
            stopDirection = Vector3.zero;
            stopSpeed = 0f;
            
            lastRecordedPlayerPos = caster.position;

            // 플레이어 공격 상태 구독 (일점사 타겟팅 지원)
            if (caster != null)
            {
                playerCombat = caster.GetComponent<PlayerCombat>();
                if (playerCombat != null)
                {
                    playerCombat.OnPlayerAttack -= HandlePlayerAttack;
                    playerCombat.OnPlayerAttack += HandlePlayerAttack;
                }

                // 플레이어 초기 체력 기록 및 피격 감지 구독
                PlayerHealth playerHealth = caster.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    lastRecordedPlayerHealth = playerHealth.CurrentHealth;
                }
                PlayerHealth.OnHealthChanged -= HandleHealthChanged;
                PlayerHealth.OnHealthChanged += HandleHealthChanged;
            }

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

            // 1. 플레이어 강제 순간이동 또는 지나친 거리에 대한 텔레포트 복귀 검사
            float distToPlayer = Vector3.Distance(transform.position, caster.position);
            float distFromLastPoint = Vector3.Distance(caster.position, lastRecordedPlayerPos);

            if (distToPlayer > leashRange || distFromLastPoint > 24f)
            {
                TeleportToPlayer();
                return;
            }

            // 순간이동 감지용으로 위치 주기적 백업
            if (distFromLastPoint >= 0.2f)
            {
                lastRecordedPlayerPos = caster.position;
            }

            // 2. 플레이어 시선 좌우 반전에 기초한 양어깨 뒤편 가상 타겟 오프셋 설정
            float targetSide = GetTargetSide();
            targetShoulderOffset = new Vector3(targetSide * 1.2f, 0.6f, 0f);

            // 오프셋 좌표를 부드럽게 Lerp하지 않고 즉시 적용하여 플레이어를 직선으로 즉시 추종하도록 변경
            currentShoulderOffset = targetShoulderOffset;

            // 3. 목적지(플레이어 본체 위치 또는 어깨 오프셋) 설정
            // 추종 기동 중일 때는 플레이어의 중심 위치를 최단 직선 목적지로 설정하여 곡선 회회 현상을 차단하고,
            // 멈춰 서 있거나 미끄러질 때는 어깨 오프셋을 목적지로 설정합니다.
            if (isMovingToTarget)
            {
                currentTargetPosition = caster.position;
            }
            else
            {
                currentTargetPosition = caster.position + currentShoulderOffset;
            }

            // 4. 소환수 중심의 불감대(데드존) 및 안착 반경 연산 (Hysteresis)
            float distanceToTarget = Vector3.Distance(transform.position, currentTargetPosition);
            float distanceToPlayer = Vector3.Distance(transform.position, caster.position);
            float proximityThreshold = 1.3f;

            if (!isMovingToTarget)
            {
                // 소환수가 멈춰 있을 때, 플레이어가 멀어져 목적지(어깨 오프셋)와의 거리가 wakeupDistance보다 커지고,
                // 플레이어가 근접 정지 임계값 밖에 있을 때만 기동 시작 (무한 기동-정지 루프 방지)
                if (distanceToTarget > wakeupDistance && distanceToPlayer > proximityThreshold)
                {
                    isMovingToTarget = true;
                    isStoppingDelayActive = false; // 기동이 재개되면 정지 지연 해제
                }
            }
            else
            {
                // 소환수가 추종 기동 중일 때
                // 목적지(플레이어 중심)에 안착 반경 이내로 도달하거나,
                // 플레이어와의 거리가 근접 정지 임계값 이내로 가까워지면 즉시 기동 정지 명령
                if (distanceToTarget <= stopRadius || distanceToPlayer <= proximityThreshold)
                {
                    isMovingToTarget = false;

                    // 정지 지연 상태 활성화 (미끄러지며 정차 시작)
                    isStoppingDelayActive = true;
                    stopDelayTimer = stopDelayDuration;
                    
                    // 펫이 멈출 때 어깨 오프셋으로 억지로 꺾지 않고 원래 가던 물리 진행 방향 그대로 미끄러지도록 설정하여 곡선 요동 현상 차단
                    if (rb != null && rb.velocity.sqrMagnitude > 0.001f)
                    {
                        stopDirection = new Vector3(rb.velocity.x, rb.velocity.y, 0f).normalized;
                    }
                    else
                    {
                        stopDirection = (caster.position - transform.position).normalized;
                    }
                    stopSpeed = currentSpeed;
                }
            }

            // 정지 지연 타이머 틱
            if (isStoppingDelayActive)
            {
                stopDelayTimer -= Time.deltaTime;
                if (stopDelayTimer <= 0f)
                {
                    isStoppingDelayActive = false;
                }
            }

            // 애니메이션 파라미터는 실제 움직임 상태이거나 정지 지연 슬라이딩 중일 때 활성화
            isFollowMoving = isMovingToTarget || isStoppingDelayActive;

            // 공격 시 일시 정지 타이머 감소
            if (attackFreezeTimer > 0f)
            {
                attackFreezeTimer -= Time.deltaTime;
            }



            // 이동 애니메이션 파라미터 업데이트 (동적 상태 플래그 상태 연동)
            if (animator != null)
            {
                bool isMoving = isFollowMoving;
                animator.SetBool(walkingBoolParam, isMoving);
            }
            lastPosition = transform.position;

            // 좌우 Flip 처리 (플레이어 방향 또는 기동 방향에 따라 결정)
            UpdateFlip();

            AutoAttackLogic();
        }

        private void FixedUpdate()
        {
            if (isActive && caster != null)
            {
                MoveDrone();
            }
        }

        private void MoveDrone()
        {
            if (attackFreezeTimer > 0f)
            {
                // 공격 중일 때는 이동을 멈춤
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                }
                currentSpeed = 0f;
                return;
            }

            if (rb == null) return;

            // 1. 기동 명령 상태일 때: 목표 위치로 속도 비례 가속 추적 실행
            if (isMovingToTarget)
            {
                Vector3 toTarget = currentTargetPosition - transform.position;
                float dist = toTarget.magnitude;

                // 목적지 거리에 따른 타겟 속도 산출 (안착지 근처 감속하되 최소 속도로 하한 제한)
                float targetSpeed = dist * maxMoveSpeed / 1.5f;
                targetSpeed = Mathf.Clamp(targetSpeed, minMoveSpeed, maxMoveSpeed);

                // 최초 기동 시작 시 최소 속도부터 시작하여 급발진 느낌 방지
                if (currentSpeed < minMoveSpeed)
                {
                    currentSpeed = minMoveSpeed;
                }

                // 설정된 가속도 단위로 목표 속도를 향해 가감속 처리
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.fixedDeltaTime * acceleration);

                Vector3 direction = toTarget.normalized;
                rb.velocity = direction * currentSpeed;
            }
            // 2. 정지 지연(슬라이딩) 상태일 때: 정지 신호가 들어왔으나 바로 정지하지 않고 약간 더 이동한 후 정지
            else if (isStoppingDelayActive)
            {
                if (stopDelayDuration > 0f)
                {
                    float progress = Mathf.Clamp01(stopDelayTimer / stopDelayDuration);
                    float slideSpeed = stopSpeed * progress;
                    rb.velocity = stopDirection * slideSpeed;
                }
                else
                {
                    rb.velocity = Vector2.zero;
                }
                currentSpeed = 0f;
            }
            // 3. 완전히 정지했을 때
            else
            {
                // 불감대 내에 있고 안착했다면 속도 정지
                rb.velocity = Vector2.zero;
                currentSpeed = 0f;
            }
        }

        /// <summary>
        /// 슬라임을 플레이어 곁으로 순간이동시키고 이펙트와 은폐 처리를 통해 연출
        /// </summary>
        private void TeleportToPlayer()
        {
            SpawnTeleportEffect(transform.position);

            float targetSide = GetTargetSide();
            targetShoulderOffset = new Vector3(targetSide * 1.2f, 0.6f, 0f);
            currentShoulderOffset = targetShoulderOffset;

            transform.position = caster.position + currentShoulderOffset;
            if (rb != null)
            {
                rb.position = transform.position;
                rb.velocity = Vector2.zero;
            }

            lastRecordedPlayerPos = caster.position;

            isFollowMoving = false;
            isMovingToTarget = false;
            isStoppingDelayActive = false;
            stopDelayTimer = 0f;
            stopDirection = Vector3.zero;
            stopSpeed = 0f;

            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(TeleportVisualRoutine());
            }
        }

        private void SpawnTeleportEffect(Vector3 position)
        {
            if (teleportEffectPrefab != null)
            {
                Instantiate(teleportEffectPrefab, position, Quaternion.identity);
            }
            else if (!string.IsNullOrEmpty(teleportEffectPoolTag) && ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.SpawnFromPool(teleportEffectPoolTag, position, Quaternion.identity);
            }
        }

        private System.Collections.IEnumerator TeleportVisualRoutine()
        {
            if (droneVisual != null)
            {
                droneVisual.SetActive(false);
            }
            yield return new WaitForSeconds(0.15f);
            if (droneVisual != null && isActive)
            {
                droneVisual.SetActive(true);
            }
        }



        /// <summary>
        /// 소환수의 이동 방향에 따라 좌우 스케일을 뒤집어 반전(Flip) 처리
        /// </summary>
        private void UpdateFlip()
        {
            if (caster == null) return;

            // 웨이포인트나 공격 조준 대신 항상 플레이어가 있는 방향을 바라보도록 설정
            float deltaX = caster.position.x - transform.position.x;
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
        /// 반응형 2순위 전투 어그로 타겟팅 (우선순위: 플레이어가 때린 적 > 피격 입힌 가해자)
        /// 유저 요청으로 인해 단순 시야 내 자동 사냥(3순위) 기능 제거됨.
        /// </summary>
        /// <returns></returns>
        private Transform FindClosestEnemy()
        {
            // [추가 최적화] 최근 3초 이내에 플레이어의 공격이나 피격 이력이 없다면 
            // 굳이 Physics2D.OverlapCircleNonAlloc 물리 연산을 수행하지 않고 즉시 리턴하여 부하를 0%로 줄입니다.
            bool isRecentOffensive = (Time.time - lastPlayerAttackTime < 3.0f);
            bool isRecentDefensive = (Time.time - lastPlayerHurtTime < 3.0f);
            if (!isRecentOffensive && !isRecentDefensive) return null;

            float range = skillData.range;

            // 지정 반경 내의 모든 2D 콜라이더 검색
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, range, droneBuffer);
            Transform closestTarget = null;

            // [우선순위 1] 플레이어가 최근에 공격(Hit)한 적 (최근 3초 이내 공격 지점 반경 3m)
            if (isRecentOffensive)
            {
                float offensiveRadius = 3f;
                float closestOffensiveDistanceSqr = Mathf.Infinity;

                for (int i = 0; i < hitCount; i++)
                {
                    Collider2D hit = droneBuffer[i];
                    if (hit.CompareTag("Enemy") && hit.GetComponent<IDamageable>() != null)
                    {
                        float distToAttackPosSqr = (hit.transform.position - lastPlayerAttackPos).sqrMagnitude;
                        if (distToAttackPosSqr < offensiveRadius * offensiveRadius)
                        {
                            if (distToAttackPosSqr < closestOffensiveDistanceSqr)
                            {
                                closestOffensiveDistanceSqr = distToAttackPosSqr;
                                closestTarget = hit.transform;
                            }
                        }
                    }
                }

                if (closestTarget != null)
                {
                    return closestTarget;
                }
            }

            // [우선순위 2] 플레이어에게 피해를 준 적 (보호 기동 - 최근 3초 이내 피격 대상)
            if (isRecentDefensive && defensiveTarget != null && defensiveTarget.gameObject.activeInHierarchy)
            {
                // 피격 가해자가 슬라임의 감지 사거리 내에 있는지 확인
                for (int i = 0; i < hitCount; i++)
                {
                    Collider2D hit = droneBuffer[i];
                    if (hit.transform == defensiveTarget)
                    {
                        return defensiveTarget;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 타겟을 향해 지정된 투사체를 오브젝트 풀에서 꺼내어 발사
        /// </summary>
        /// <param name="target"></param>
        private void FireAtTarget(Transform target)
        {
            // 공격을 가하기 시작하는 순간의 위치 고정 및 이동 정지 설정
            attackFreezeTimer = attackFreezeDuration;

            // 발사 방향 계산
            Vector2 direction = (target.position - transform.position).normalized;

            // (상시 플레이어를 바라보는 사양에 맞춰 사격 시의 갑작스러운 로컬 회전 및 전환 효과를 배제합니다)

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

                // 소환수가 발사하는 투사체는 기본적으로 관통 속성을 끄도록 예외 처리 
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

        private void HandlePlayerAttack(Vector2 direction, Vector3 targetPosition)
        {
            lastPlayerAttackPos = targetPosition;
            lastPlayerAttackTime = Time.time;
        }

        private void HandleHealthChanged(float currentHealth, float maxHealth)
        {
            // 체력이 감소한 경우 피격으로 판단
            if (lastRecordedPlayerHealth > 0f && currentHealth < lastRecordedPlayerHealth)
            {
                FindPlayerAttacker();
            }
            lastRecordedPlayerHealth = currentHealth;
        }

        private void FindPlayerAttacker()
        {
            if (caster == null) return;

            // 플레이어 주변 반경 5m 내의 적을 탐색
            int hitCount = Physics2D.OverlapCircleNonAlloc(caster.position, 5f, droneBuffer);
            float closestDistanceSqr = Mathf.Infinity;
            Transform closestAttacker = null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = droneBuffer[i];
                if (hit.CompareTag("Enemy") && hit.GetComponent<IDamageable>() != null)
                {
                    float distanceSqr = (caster.position - hit.transform.position).sqrMagnitude;
                    if (distanceSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distanceSqr;
                        closestAttacker = hit.transform;
                    }
                }
            }

            if (closestAttacker != null)
            {
                defensiveTarget = closestAttacker;
                lastPlayerHurtTime = Time.time;
            }
        }

        private void DeactivateDrone()
        {
            if (!isActive) return;

            isActive = false;

            if (playerCombat != null)
            {
                playerCombat.OnPlayerAttack -= HandlePlayerAttack;
            }
            PlayerHealth.OnHealthChanged -= HandleHealthChanged;

            if (animator != null)
            {
                animator.SetBool(walkingBoolParam, false);
                animator.SetTrigger(deathTriggerParam);
            }
            else
            {
                // 애니메이터가 없는 경우 예외 처리를 위해 즉시 비활성화
                OnDeathAnimationEnd();
            }
        }

        private void OnDestroy()
        {
            if (playerCombat != null)
            {
                playerCombat.OnPlayerAttack -= HandlePlayerAttack;
            }
            PlayerHealth.OnHealthChanged -= HandleHealthChanged;
        }

        private float GetTargetSide()
        {
            if (caster == null) return -1f;

            // 1. PlayerController 입력 상태를 최우선으로 검사 (디지털 입력값으로 감속 노이즈 차단)
            PlayerController playerController = caster.GetComponent<PlayerController>();
            if (playerController != null)
            {
                if (Mathf.Abs(playerController.MoveInput.x) > 0.1f)
                {
                    return playerController.MoveInput.x > 0.1f ? -1f : 1f;
                }
                
                // 정지해 있을 때는 시선 방향 기준으로 셋팅
                return playerController.IsFacingRight ? -1f : 1f;
            }

            // 2. PlayerController가 없는 경우에만 물리 속도(Rigidbody2D)로 판정
            Rigidbody2D playerRb = caster.GetComponent<Rigidbody2D>();
            if (playerRb != null && Mathf.Abs(playerRb.velocity.x) > 0.1f)
            {
                return playerRb.velocity.x > 0.1f ? -1f : 1f;
            }

            SpriteRenderer casterSprite = caster.GetComponent<SpriteRenderer>();
            if (casterSprite != null)
            {
                return casterSprite.flipX ? 1f : -1f;
            }

            return (caster.localScale.x > 0f) ? -1f : 1f;
        }

        /// <summary>
        /// 사망 애니메이션 클립의 마지막 프레임 이벤트에서 호출되어 슬라임을 비활성화 처리하는 함수
        /// </summary>
        public void OnDeathAnimationEnd()
        {
            if (droneVisual != null)
            {
                droneVisual.SetActive(false);
            }
        }
    }
}