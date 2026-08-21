using System.Collections;
using Nytherion.GamePlay.Combat.Behaviors;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class FrogJumpMovement : MonoBehaviour
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
        [SerializeField] private float jumpMoveSpeed = 18f;
        [SerializeField] private float jumpAcceleration = 200f;

        private readonly NavMeshPath jumpPath = new NavMeshPath();

        private float originalAgentSpeed;
        private float originalAgentAcceleration;
        private bool isJumping;
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
            HideAttackRangePreview();
            landingAttack?.DeactivateCollider();
        }

        // Run 애니메이션의 점프 시작 프레임에서 호출
        public void FrogJumpStart()
        {
            if (!TryGetLandingPosition(out Vector3 landingPosition))
            {
                enemyAIController?.ClearForcedDestination();
                enemyAIController?.SetMovementAllowed(true);
                HideAttackRangePreview();
                return;
            }

            NavMeshAgent agent = enemyAIController.agent;

            originalAgentSpeed = agent.speed;
            originalAgentAcceleration = agent.acceleration;

            agent.speed = jumpMoveSpeed;
            agent.acceleration = jumpAcceleration;

            isJumping = true;

            enemyAIController.SetForcedDestination(landingPosition);
            enemyAIController.SetMovementAllowed(true);

            ShowAttackRangePreview(landingPosition);
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
            HideAttackRangePreview();

            if (isJumping && enemyAIController != null && enemyAIController.agent != null)
            {
                enemyAIController.agent.speed = originalAgentSpeed;
                enemyAIController.agent.acceleration = originalAgentAcceleration;
            }

            isJumping = false;

            enemyAIController?.ClearForcedDestination();
            enemyAIController?.SetMovementAllowed(false);

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
            attackRangePreview.transform.position = position;
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