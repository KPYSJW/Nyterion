using System.Collections;
using Nytherion.GamePlay.Combat.Behaviors;
using UnityEngine;

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
        }

        private void OnEnable()
        {
            enemyAIController?.SetMovementAllowed(false);
            landingAttack?.DeactivateCollider();
            attackRangePreview?.SetActive(false);
            landingEffect?.SetActive(false);

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
            attackRangePreview?.SetActive(false);
            landingEffect?.SetActive(false);
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
            enemyAIController?.SetMovementAllowed(true);
            attackRangePreview?.SetActive(true);
        }

        // Run 애니메이션의 착지 프레임에서 호출
        public void FrogLand()
        {
            attackRangePreview?.SetActive(false);
            enemyAIController?.SetMovementAllowed(false);
            landingAttack?.ActivateCollider();
            PlayLandingEffect();
        }
        public void FrogStartIdle()
        {
            landingAttack?.DeactivateCollider();

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

        
    }
}
