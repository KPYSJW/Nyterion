using System.Collections;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Combat.Behaviors;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class FrogJumpMovement : MonoBehaviour, IEnemyMovementBehavior
    {
        private enum JumpType
        {
            Attack,
            Movement
        }

        [Header("References")]
        [SerializeField] private EnemyAIController enemyAIController;
        [SerializeField] private Animator animator;
        [SerializeField] private MeleeAttackBehavior landingAttack;

        [Header("Attack Rhythm")]
        [Min(0f)]
        [SerializeField] private float postAttackIdleDuration = 1f;
        [Min(0f)]
        [SerializeField] private float movementHopIdleDuration = 0.2f;
        [Min(0f)]
        [SerializeField] private float movementHopRandomDelay = 0.15f;
        [Min(1)]
        [SerializeField] private int minimumMovementJumps = 1;
        [Min(1)]
        [SerializeField] private int maximumMovementJumps = 3;

        [Header("Jump Distance")]
        [Min(0f)]
        [SerializeField] private float movementJumpDistance = 1.5f;
        [Min(0f)]
        [SerializeField] private float attackJumpDistance = 4f;

        [Header("Landing Offset")]
        [Min(0f)]
        [SerializeField] private float maximumLandingOffset = 0.3f;
        [Range(0f, 1f)]
        [SerializeField] private float landingSeparationStartRatio = 0.75f;

        [Header("Attack Range Preview")]
        [SerializeField] private GameObject attackRangePreview;

        [Header("Landing Effect")]
        [SerializeField] private GameObject landingEffect;
        [SerializeField] private Animator landingEffectAnimator;

        [Header("Attack Airborne Visual")]
        [Min(0f)]
        [SerializeField] private float attackVisualHeight = 2.5f;
        [Range(0.1f, 0.8f)]
        [Tooltip("공격 점프가 목표 지점 위에 도착하는 진행률입니다.")]
        [SerializeField] private float attackApproachEndRatio = 0.35f;
        [Range(0.2f, 0.95f)]
        [Tooltip("목표 지점 위에서 체공한 뒤 급강하를 시작하는 진행률입니다.")]
        [SerializeField] private float attackSlamStartRatio = 0.8f;

        [Header("Jump Timing")]
        [Min(0.01f)]
        [Tooltip("MoveJump의 FrogJumpStart와 FrogLand 이벤트 사이의 시간입니다.")]
        [SerializeField] private float jumpTravelDuration = 0.46666667f;
        [Min(0.01f)]
        [Tooltip("공격 Run의 FrogJumpStart와 FrogLand 이벤트 사이의 시간입니다.")]
        [SerializeField] private float attackJumpTravelDuration = 0.8666667f;
        [Tooltip("점프 시간 진행률을 실제 이동 거리 진행률로 변환합니다.")]
        [SerializeField] private AnimationCurve jumpMoveCurve;

        private NavMeshPath jumpPath;
        private Vector3[] jumpCorners;
        private float totalJumpPathLength;
        private float jumpStartTime;
        private bool originalAgentUpdatePosition;
        private ObstacleAvoidanceType originalAvoidanceType;
        private bool isJumping;
        private Vector3 currentLandingPosition;
        private Vector3 currentPathLandingPosition;
        private Vector3 landingSeparationOffset;
        private bool hasLandingPosition;
        private JumpType currentJumpType = JumpType.Movement;
        private int remainingMovementJumps;
        private Transform visualTransform;
        private Vector3 originalVisualLocalPosition;
        private Collider2D bodyCollider;
        private Vector2 originalBodyColliderOffset;
        private bool originalBodyColliderIsTrigger;
        private Collider2D ignoredPlayerBodyCollider;
        private bool shouldRestorePlayerCollision;
        private bool attackAirborneStateActive;

        public bool RequiresNavMeshAgent => true;
        public bool IsJumping => isJumping;
        public Vector3 CurrentVelocity =>
            enemyAIController != null && enemyAIController.agent != null
                ? enemyAIController.agent.velocity
                : Vector3.zero;
        private Transform previewOriginalParent;
        private Coroutine landingRoutine;

        private void Reset()
        {
            enemyAIController = GetComponent<EnemyAIController>();
            landingAttack = GetComponent<MeleeAttackBehavior>();

            if (enemyAIController != null)
            {
                animator = enemyAIController.animator;
            }
        }

        private void Awake()
        {
            jumpPath = new NavMeshPath();

            if (jumpMoveCurve == null || jumpMoveCurve.length == 0)
            {
                jumpMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (enemyAIController == null)
            {
                enemyAIController = GetComponent<EnemyAIController>();
            }

            if (landingAttack == null)
            {
                landingAttack = GetComponent<MeleeAttackBehavior>();
            }

            if (animator == null && enemyAIController != null)
            {
                animator = enemyAIController.animator;
            }

            visualTransform = animator != null ? animator.transform : null;
            if (visualTransform != null)
            {
                originalVisualLocalPosition = visualTransform.localPosition;
            }

            bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider != null)
            {
                originalBodyColliderOffset = bodyCollider.offset;
                originalBodyColliderIsTrigger = bodyCollider.isTrigger;
            }

            if (attackRangePreview != null)
            {
                previewOriginalParent = attackRangePreview.transform.parent;
            }
        }

        private void Update()
        {
            if (!isJumping || !hasLandingPosition)
            {
                return;
            }

            float elapsedTime = Time.time - jumpStartTime;
            float travelDuration = currentJumpType == JumpType.Attack
                ? attackJumpTravelDuration
                : jumpTravelDuration;
            float timeRatio = Mathf.Clamp01(elapsedTime / travelDuration);
            float distanceRatio;
            float separationRatio;

            if (currentJumpType == JumpType.Attack)
            {
                float approachRatio = Mathf.Clamp01(
                    timeRatio / Mathf.Max(0.01f, attackApproachEndRatio));
                distanceRatio = Mathf.SmoothStep(0f, 1f, approachRatio);
                separationRatio = distanceRatio;
            }
            else
            {
                distanceRatio = Mathf.Clamp01(jumpMoveCurve.Evaluate(timeRatio));
                separationRatio = Mathf.InverseLerp(
                    landingSeparationStartRatio,
                    1f,
                    timeRatio);
                separationRatio = Mathf.SmoothStep(0f, 1f, separationRatio);
            }

            Vector3 nextPosition = GetPositionAlongPath(totalJumpPathLength * distanceRatio);
            nextPosition += landingSeparationOffset * separationRatio;

            ApplyJumpPosition(nextPosition);
            UpdateAttackAirborneVisual(timeRatio);

            if (timeRatio >= 1f)
            {
                ApplyJumpPosition(currentLandingPosition);
            }
        }

        public void Configure(EnemyData data)
        {
            if (data != null &&
                enemyAIController != null &&
                enemyAIController.agent != null)
            {
                enemyAIController.agent.speed = data.moveSpeed;
            }
        }

        public void MoveTowards(Vector3 destination)
        {
            if (!isJumping)
            {
                StopMovement();
            }
        }

        public void StopMovement()
        {
            if (enemyAIController == null)
                return;

            NavMeshAgent agent = enemyAIController.agent;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            if (enemyAIController.rb != null)
            {
                enemyAIController.rb.velocity = Vector2.zero;
            }
        }

        private void OnEnable()
        {
            currentJumpType = JumpType.Movement;
            RollMovementJumpCount();
            enemyAIController?.SetMovementAllowed(false);
            landingAttack?.DeactivateCollider();
            HideAttackRangePreview();

            if (animator != null)
            {
                animator.Play("Idle", 0, 0f);
            }
        }

        private void OnDisable()
        {
            if (landingRoutine != null)
            {
                StopCoroutine(landingRoutine);
                landingRoutine = null;
            }

            RestoreAttackAirborneState();
            RestoreAgentSettings();
            hasLandingPosition = false;
            HideAttackRangePreview();
            landingAttack?.DeactivateCollider();
        }

        public void ResetForReuse()
        {
            if (landingRoutine != null)
            {
                StopCoroutine(landingRoutine);
                landingRoutine = null;
            }

            RestoreAttackAirborneState();
            enemyAIController?.SetMovementAllowed(false);
            RestoreAgentSettings();
            hasLandingPosition = false;
            currentJumpType = JumpType.Movement;
            RollMovementJumpCount();
            landingAttack?.ResetForReuse();
            attackRangePreview?.SetActive(false);
            landingEffect?.SetActive(false);

            if (animator != null)
            {
                animator.Play("Idle", 0, 0f);
            }
        }

        // Run 애니메이션의 점프 시작 프레임에서 호출
        public void FrogJumpStart()
        {
            float jumpDistance = currentJumpType == JumpType.Attack
                ? attackJumpDistance
                : movementJumpDistance;

            if (!TryGetLandingPosition(jumpDistance, out Vector3 desiredLandingPosition))
            {
                CancelJumpStart();
                return;
            }

            Vector3 landingPosition = FindOffsetLandingPosition(
                desiredLandingPosition);

            NavMeshAgent agent = enemyAIController.agent;

            // 공중에서는 원래 목표 경로를 함께 사용하고, 착지 직전에만
            // 분산 위치로 부드럽게 벌어진다.
            if (!agent.CalculatePath(desiredLandingPosition, jumpPath) ||
                jumpPath.status != NavMeshPathStatus.PathComplete)
            {
                CancelJumpStart();
                return;
            }

            Vector3[] calculatedCorners = jumpPath.corners;
            float travelDistance = GetPathLength(calculatedCorners);
            if (calculatedCorners == null ||
                calculatedCorners.Length < 2 ||
                travelDistance <= 0.001f)
            {
                CancelJumpStart();
                return;
            }

            originalAgentUpdatePosition = agent.updatePosition;
            originalAvoidanceType = agent.obstacleAvoidanceType;

            enemyAIController.ClearForcedDestination();
            enemyAIController.SetMovementAllowed(false);

            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.updatePosition = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            jumpCorners = calculatedCorners;
            totalJumpPathLength = travelDistance;
            jumpStartTime = Time.time;
            isJumping = true;
            currentLandingPosition = landingPosition;
            currentPathLandingPosition = desiredLandingPosition;
            landingSeparationOffset =
                currentLandingPosition - currentPathLandingPosition;
            hasLandingPosition = true;

            if (currentJumpType == JumpType.Attack)
            {
                BeginAttackAirborneState();
            }

            if (currentJumpType == JumpType.Attack)
            {
                ShowAttackRangePreview(currentLandingPosition);
            }
            else
            {
                HideAttackRangePreview();
            }
        }

        private void CancelJumpStart()
        {
            isJumping = false;
            hasLandingPosition = false;
            jumpCorners = null;
            totalJumpPathLength = 0f;
            jumpStartTime = 0f;
            currentPathLandingPosition = transform.position;
            landingSeparationOffset = Vector3.zero;
            RestoreAttackAirborneState();

            enemyAIController?.ClearForcedDestination();
            enemyAIController?.SetMovementAllowed(false);
            HideAttackRangePreview();
        }

        private float GetPathLength(Vector3[] corners)
        {
            if (corners == null || corners.Length < 2)
            {
                return 0f;
            }

            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        private Vector3 GetPositionAlongPath(float distance)
        {
            if (jumpCorners == null || jumpCorners.Length == 0)
            {
                return transform.position;
            }

            float remainingDistance = Mathf.Clamp(distance, 0f, totalJumpPathLength);

            for (int i = 1; i < jumpCorners.Length; i++)
            {
                Vector3 start = jumpCorners[i - 1];
                Vector3 end = jumpCorners[i];
                float segmentLength = Vector3.Distance(start, end);

                if (segmentLength <= 0.001f)
                {
                    continue;
                }

                if (remainingDistance <= segmentLength)
                {
                    return Vector3.Lerp(start, end, remainingDistance / segmentLength);
                }

                remainingDistance -= segmentLength;
            }

            return currentPathLandingPosition;
        }

        private void ApplyJumpPosition(Vector3 position)
        {
            transform.position = position;

            if (enemyAIController != null)
            {
                if (enemyAIController.rb != null)
                {
                    enemyAIController.rb.position = position;
                }

                NavMeshAgent agent = enemyAIController.agent;
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.nextPosition = position;
                }
            }
        }

        private void RestoreAgentSettings()
        {
            if (!isJumping || enemyAIController == null || enemyAIController.agent == null)
            {
                return;
            }

            NavMeshAgent agent = enemyAIController.agent;
            Vector3 currentPosition = transform.position;

            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.nextPosition = currentPosition;
                agent.updatePosition = originalAgentUpdatePosition;
                agent.Warp(currentPosition);
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            else
            {
                agent.updatePosition = originalAgentUpdatePosition;
            }

            agent.obstacleAvoidanceType = originalAvoidanceType;

            isJumping = false;
            jumpCorners = null;
            totalJumpPathLength = 0f;
            jumpStartTime = 0f;
            currentPathLandingPosition = currentPosition;
            landingSeparationOffset = Vector3.zero;
        }

        private void BeginAttackAirborneState()
        {
            if (attackAirborneStateActive)
            {
                return;
            }

            attackAirborneStateActive = true;

            if (bodyCollider != null)
            {
                originalBodyColliderOffset = bodyCollider.offset;
                originalBodyColliderIsTrigger = bodyCollider.isTrigger;
                bodyCollider.isTrigger = true;
            }

            ignoredPlayerBodyCollider =
                enemyAIController != null && enemyAIController.player != null
                    ? enemyAIController.player.GetComponent<Collider2D>()
                    : null;

            shouldRestorePlayerCollision =
                bodyCollider != null &&
                ignoredPlayerBodyCollider != null &&
                !Physics2D.GetIgnoreCollision(
                    bodyCollider,
                    ignoredPlayerBodyCollider);

            if (shouldRestorePlayerCollision)
            {
                Physics2D.IgnoreCollision(
                    bodyCollider,
                    ignoredPlayerBodyCollider,
                    true);
            }

            UpdateAttackAirborneVisual(0f);
        }

        private void UpdateAttackAirborneVisual(float timeRatio)
        {
            if (!attackAirborneStateActive || currentJumpType != JumpType.Attack)
            {
                return;
            }

            float clampedTimeRatio = Mathf.Clamp01(timeRatio);
            float approachEndRatio = Mathf.Clamp(
                attackApproachEndRatio,
                0.01f,
                0.95f);
            float slamStartRatio = Mathf.Clamp(
                attackSlamStartRatio,
                approachEndRatio,
                0.99f);
            float heightRatio;

            if (clampedTimeRatio < approachEndRatio)
            {
                heightRatio = Mathf.SmoothStep(
                    0f,
                    1f,
                    clampedTimeRatio / approachEndRatio);
            }
            else if (clampedTimeRatio < slamStartRatio)
            {
                heightRatio = 1f;
            }
            else
            {
                float slamRatio = Mathf.InverseLerp(
                    slamStartRatio,
                    1f,
                    clampedTimeRatio);
                heightRatio = 1f - slamRatio * slamRatio;
            }

            float height = attackVisualHeight * heightRatio;

            if (visualTransform != null)
            {
                visualTransform.localPosition =
                    originalVisualLocalPosition + Vector3.up * height;
            }

            if (bodyCollider != null)
            {
                bodyCollider.offset =
                    originalBodyColliderOffset + Vector2.up * height;
            }
        }

        private void RestoreAttackAirborneState()
        {
            if (visualTransform != null)
            {
                visualTransform.localPosition = originalVisualLocalPosition;
            }

            if (bodyCollider != null)
            {
                bodyCollider.offset = originalBodyColliderOffset;
                bodyCollider.isTrigger = originalBodyColliderIsTrigger;
            }

            if (shouldRestorePlayerCollision &&
                bodyCollider != null &&
                ignoredPlayerBodyCollider != null)
            {
                Physics2D.IgnoreCollision(
                    bodyCollider,
                    ignoredPlayerBodyCollider,
                    false);
            }

            ignoredPlayerBodyCollider = null;
            shouldRestorePlayerCollision = false;
            attackAirborneStateActive = false;
        }

        private bool TryGetLandingPosition(
            float jumpDistance,
            out Vector3 landingPosition)
        {
            landingPosition = transform.position;

            if (enemyAIController == null ||
                enemyAIController.agent == null ||
                !enemyAIController.agent.isOnNavMesh ||
                enemyAIController.player == null)
            {
                return false;
            }

            bool hasPath = enemyAIController.agent.CalculatePath(
                enemyAIController.player.position,
                jumpPath);

            if (!hasPath ||
                jumpPath.status != NavMeshPathStatus.PathComplete ||
                jumpPath.corners == null ||
                jumpPath.corners.Length < 2)
            {
                return false;
            }

            float remainingDistance = jumpDistance;

            for (int i = 1; i < jumpPath.corners.Length; i++)
            {
                Vector3 start = jumpPath.corners[i - 1];
                Vector3 end = jumpPath.corners[i];

                float segmentLength = Vector3.Distance(start, end);

                if (segmentLength >= remainingDistance)
                {
                    landingPosition = Vector3.Lerp(
                        start,
                        end,
                        remainingDistance / segmentLength);

                    return true;
                }

                remainingDistance -= segmentLength;
            }

            // 플레이어가 점프 거리보다 가까우면,
            // 경로의 마지막 유효 지점까지만 이동한다.
            landingPosition = jumpPath.corners[jumpPath.corners.Length - 1];
            return true;
        }

        private Vector3 FindOffsetLandingPosition(Vector3 desiredPosition)
        {
            NavMeshAgent agent = enemyAIController.agent;
            if (maximumLandingOffset <= 0f)
            {
                return desiredPosition;
            }

            uint hash = unchecked((uint)GetInstanceID());
            hash ^= hash >> 16;
            hash *= 0x7feb352d;
            hash ^= hash >> 15;
            hash *= 0x846ca68b;
            hash ^= hash >> 16;

            float angleRatio = (hash & 0xffff) / 65535f;
            float radiusRatio = ((hash >> 16) & 0xffff) / 65535f;
            float angle = angleRatio * Mathf.PI * 2f;
            float radius = maximumLandingOffset * Mathf.Sqrt(radiusRatio);
            Vector3 offset = new Vector3(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                0f) * radius;

            if (!NavMesh.SamplePosition(
                    desiredPosition + offset,
                    out NavMeshHit navMeshHit,
                    maximumLandingOffset,
                    agent.areaMask))
            {
                return desiredPosition;
            }

            Vector3 candidate = navMeshHit.position;
            if (NavMesh.Raycast(
                    desiredPosition,
                    candidate,
                    out _,
                    agent.areaMask) ||
                !agent.CalculatePath(candidate, jumpPath) ||
                jumpPath.status != NavMeshPathStatus.PathComplete)
            {
                return desiredPosition;
            }

            return candidate;
        }
        // Run 애니메이션의 착지 프레임에서 호출
       public void FrogLand()
        {
            bool completedMovementJump =
                currentJumpType == JumpType.Movement &&
                isJumping &&
                hasLandingPosition;

            // 수동 경로 이동의 마지막 값을 빨간 원과 동일한 착륙 위치로 확정한다.
            if (isJumping && hasLandingPosition)
            {
                ApplyJumpPosition(currentLandingPosition);
            }

            UpdateAttackAirborneVisual(1f);

            hasLandingPosition = false;
            HideAttackRangePreview();

            // 착륙 위치를 확정한 뒤 모든 이동 요청과 남은 속도를 제거한다.
            enemyAIController?.ClearForcedDestination();
            enemyAIController?.SetMovementAllowed(false);

            // 현재 위치에서 Agent 내부 좌표만 동기화하고 자동 위치 제어를 복구한다.
            RestoreAgentSettings();

            if (completedMovementJump)
            {
                remainingMovementJumps = Mathf.Max(
                    0,
                    remainingMovementJumps - 1);
            }

            if (currentJumpType == JumpType.Attack)
            {
                landingAttack?.ActivateCollider();
                PlayLandingEffect();
            }
            else
            {
                landingAttack?.DeactivateCollider();
            }
        }
        public void FrogStartIdle()
        {
            RestoreAttackAirborneState();
            HideAttackRangePreview();

            if (landingRoutine != null)
            {
                StopCoroutine(landingRoutine);
            }

            if (animator != null)
            {
                animator.Play("Idle", 0, 0f);
            }

            if (currentJumpType == JumpType.Attack)
            {
                landingRoutine = StartCoroutine(PostAttackIdleRoutine());
                return;
            }

            landingRoutine = StartCoroutine(MovementHopIdleRoutine());
        }

        private IEnumerator PostAttackIdleRoutine()
        {
            yield return new WaitForSeconds(postAttackIdleDuration);

            currentJumpType = JumpType.Movement;
            RollMovementJumpCount();
            PlayCurrentJumpAnimation();

            landingRoutine = null;
        }

        private void ContinueMovementOrAttack()
        {
            currentJumpType = remainingMovementJumps <= 0
                ? JumpType.Attack
                : JumpType.Movement;

            PlayCurrentJumpAnimation();
        }

        private void PlayCurrentJumpAnimation()
        {
            if (animator == null)
            {
                return;
            }

            string stateName = currentJumpType == JumpType.Attack
                ? "AttackJump"
                : "Run";
            animator.Play(stateName, 0, 0f);
        }

        private void RollMovementJumpCount()
        {
            int minimum = Mathf.Max(1, minimumMovementJumps);
            int maximum = Mathf.Max(minimum, maximumMovementJumps);
            remainingMovementJumps = Random.Range(minimum, maximum + 1);
        }

        private IEnumerator MovementHopIdleRoutine()
        {
            float randomDelay = movementHopRandomDelay > 0f
                ? Random.Range(0f, movementHopRandomDelay)
                : 0f;
            yield return new WaitForSeconds(
                movementHopIdleDuration + randomDelay);

            ContinueMovementOrAttack();
            landingRoutine = null;
        }
        private void PlayLandingEffect()
        {
            if (landingEffect == null || landingEffectAnimator == null)
            {
                return;
            }

            landingEffect.SetActive(true);
            landingEffectAnimator.Play("Effect", 0, 0f);
        }

        private void ShowAttackRangePreview(Vector3 position)
        {
            if (attackRangePreview == null)
            {
                return;
            }

            attackRangePreview.transform.SetParent(null, true);
            attackRangePreview.transform.position = position+new Vector3(0,-0.55f,0);
            attackRangePreview.SetActive(true);
        }

        private void HideAttackRangePreview()
        {
            if (attackRangePreview == null)
            {
                return;
            }

            attackRangePreview.SetActive(false);

            if (previewOriginalParent != null)
            {
                attackRangePreview.transform.SetParent(previewOriginalParent, false);
            }
        }
    }
}
