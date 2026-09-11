using System.Collections;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Combat.Behaviors;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class FrogJumpMovement : MonoBehaviour, IEnemyMovementBehavior
    {
        [Header("References")]
        [SerializeField] private EnemyAIController enemyAIController;
        [SerializeField] private Animator animator;
        [SerializeField] private MeleeAttackBehavior landingAttack;

        [Header("Landing Settings")]
        [Min(0f)]
        [SerializeField] private float landingIdleDuration = 0.5f;

        [Header("Attack Range Preview")]
        [SerializeField] private GameObject attackRangePreview;

        [Header("Landing Effect")]
        [SerializeField] private GameObject landingEffect;
        [SerializeField] private Animator landingEffectAnimator;

        [Header("Jump Target Settings")]
        [SerializeField] private float jumpPathDistance = 6f;

        [Header("Jump Timing")]
        [Min(0.01f)]
        [Tooltip("FrogJumpStart와 FrogLand 애니메이션 이벤트 사이의 시간입니다.")]
        [SerializeField] private float jumpTravelDuration = 0.46666667f;
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
        private bool hasLandingPosition;

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
            float timeRatio = Mathf.Clamp01(elapsedTime / jumpTravelDuration);
            float distanceRatio = Mathf.Clamp01(jumpMoveCurve.Evaluate(timeRatio));
            Vector3 nextPosition = GetPositionAlongPath(totalJumpPathLength * distanceRatio);

            ApplyJumpPosition(nextPosition);

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

            enemyAIController?.SetMovementAllowed(false);
            RestoreAgentSettings();
            hasLandingPosition = false;
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
            if (!TryGetLandingPosition(out Vector3 landingPosition))
            {
                CancelJumpStart();
                return;
            }

            NavMeshAgent agent = enemyAIController.agent;

            if (!agent.CalculatePath(landingPosition, jumpPath) ||
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
            hasLandingPosition = true;

            ShowAttackRangePreview(currentLandingPosition);
        }

        private void CancelJumpStart()
        {
            isJumping = false;
            hasLandingPosition = false;
            jumpCorners = null;
            totalJumpPathLength = 0f;
            jumpStartTime = 0f;

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

            return currentLandingPosition;
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
        }

        private bool TryGetLandingPosition(out Vector3 landingPosition)
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

            float remainingDistance = jumpPathDistance;

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
        // Run 애니메이션의 착지 프레임에서 호출
       public void FrogLand()
        {
            // 수동 경로 이동의 마지막 값을 빨간 원과 동일한 착륙 위치로 확정한다.
            if (isJumping && hasLandingPosition)
            {
                ApplyJumpPosition(currentLandingPosition);
            }

            hasLandingPosition = false;
            HideAttackRangePreview();

            // 착륙 위치를 확정한 뒤 모든 이동 요청과 남은 속도를 제거한다.
            enemyAIController?.ClearForcedDestination();
            enemyAIController?.SetMovementAllowed(false);

            // 현재 위치에서 Agent 내부 좌표만 동기화하고 자동 위치 제어를 복구한다.
            RestoreAgentSettings();

            landingAttack?.ActivateCollider();
            PlayLandingEffect();
        }
        public void FrogStartIdle()
        {
            HideAttackRangePreview();

            if (landingRoutine != null)
            {
                StopCoroutine(landingRoutine);
            }

            if (animator != null)
            {
                animator.Play("Idle", 0, 0f);
            }

            landingRoutine = StartCoroutine(IdleRoutine());
        }

        private IEnumerator IdleRoutine()
        {
            yield return new WaitForSeconds(landingIdleDuration);

            if (animator != null)
            {
                animator.Play("Run", 0, 0f);
            }

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
