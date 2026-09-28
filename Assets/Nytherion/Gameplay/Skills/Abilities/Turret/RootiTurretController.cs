using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 포탑 스킬로 날아가 착지한 뒤 고정된 자리에서 씨앗을 발사하는 루티입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RootiTurretController : TurretController
    {
        private enum DeploymentState { Flying, Landing, Ready, Attacking }

        [Header("배치 애니메이션")]
        [SerializeField] private GameObject visual;
        [SerializeField, Min(0.01f)] private float flightDuration = 0.45f;
        [SerializeField, Min(0f)] private float flightHeight = 0.8f;
        [SerializeField, Min(0.01f)] private float landingDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float attackAnimationDuration = 0.5f;

        private static readonly Collider2D[] EnemyBuffer = new Collider2D[20];
        private Animator animator;
        private Transform visualTransform;
        private Vector3 visualRestPosition;
        private Vector3 launchPosition;
        private Vector3 landingPosition;
        private float phaseStartTime;
        private DeploymentState deploymentState;
        private Transform pendingTarget;
        private bool isSeedLaunchPending;

        public override void Initialize(TurretSkillData data)
        {
            base.Initialize(data);
            CacheVisual();
            deploymentState = DeploymentState.Ready;
            pendingTarget = null;
            isSeedLaunchPending = false;
            if (TryGetComponent(out Rigidbody2D rigidbody))
            {
                rigidbody.velocity = Vector2.zero;
                rigidbody.bodyType = RigidbodyType2D.Kinematic;
                rigidbody.gravityScale = 0f;
            }
        }

        public override void Deploy(Vector3 startPosition, Vector3 destination)
        {
            CacheVisual();
            launchPosition = startPosition;
            landingPosition = destination;
            transform.position = startPosition;
            phaseStartTime = Time.time;
            deploymentState = DeploymentState.Flying;
            FacePosition(destination);
            PlayAnimation("Jump");
        }

        protected override void Update()
        {
            float elapsed = Time.time - phaseStartTime;
            if (deploymentState == DeploymentState.Flying)
            {
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, flightDuration));
                transform.position = Vector3.Lerp(launchPosition, landingPosition, progress);
                if (visualTransform != null && visualTransform != transform)
                {
                    visualTransform.localPosition = visualRestPosition +
                        Vector3.up * (4f * progress * (1f - progress) * flightHeight);
                }
                if (progress >= 1f)
                {
                    transform.position = landingPosition;
                    ResetVisualPosition();
                    deploymentState = DeploymentState.Landing;
                    phaseStartTime = Time.time;
                    PlayAnimation("Landed");
                }
                return;
            }
            if (deploymentState == DeploymentState.Landing)
            {
                if (elapsed >= landingDuration) CompleteLanding();
                return;
            }
            if (deploymentState == DeploymentState.Attacking)
            {
                if (pendingTarget != null) FacePosition(pendingTarget.position);
                if (elapsed >= attackAnimationDuration) CompleteAttack();
            }
            base.Update();
        }

        protected override void PerformAttack()
        {
            if (deploymentState != DeploymentState.Ready) return;

            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, attackRange, EnemyBuffer);
            Transform closestTarget = null;
            float closestDistanceSqr = Mathf.Infinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = EnemyBuffer[i];
                if (hit == null || !hit.CompareTag("Enemy") || hit.GetComponent<IDamageable>() == null) continue;
                float distanceSqr = (hit.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestTarget = hit.transform;
                }
            }
            if (closestTarget == null) return;

            pendingTarget = closestTarget;
            isSeedLaunchPending = true;
            deploymentState = DeploymentState.Attacking;
            phaseStartTime = Time.time;
            FacePosition(closestTarget.position);
            PlayAnimation("Flower Whip Attack");
        }

        /// <summary>
        /// Flower Whip Attack의 세 번째 프레임 Animation Event에서 호출됩니다.
        /// </summary>
        public void LaunchSeed()
        {
            if (deploymentState != DeploymentState.Attacking || !isSeedLaunchPending)
            {
                return;
            }

            isSeedLaunchPending = false;
            if (pendingTarget != null && pendingTarget.gameObject.activeInHierarchy)
            {
                FacePosition(pendingTarget.position);
                LaunchProjectileAtTarget(pendingTarget);
            }
        }

        public void CompleteLanding()
        {
            if (deploymentState != DeploymentState.Landing) return;
            deploymentState = DeploymentState.Ready;
            ResetVisualPosition();
            PlayAnimation("Idle");
        }

        public void CompleteAttack()
        {
            if (deploymentState != DeploymentState.Attacking) return;
            pendingTarget = null;
            isSeedLaunchPending = false;
            deploymentState = DeploymentState.Ready;
            PlayAnimation("Idle");
        }

        private void CacheVisual()
        {
            if (visualTransform == null)
            {
                visualTransform = visual != null ? visual.transform : transform;
                visualRestPosition = visualTransform.localPosition;
                animator = visualTransform.GetComponent<Animator>();
            }
        }

        private void FacePosition(Vector3 position)
        {
            float difference = position.x - transform.position.x;
            if (Mathf.Abs(difference) > 0.01f)
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (difference > 0f ? 1f : -1f);
                transform.localScale = scale;
            }
        }

        private void PlayAnimation(string stateName)
        {
            if (animator != null) animator.Play(stateName, 0, 0f);
        }

        private void ResetVisualPosition()
        {
            if (visualTransform != null && visualTransform != transform)
            {
                visualTransform.localPosition = visualRestPosition;
            }
        }

        private void OnDisable()
        {
            pendingTarget = null;
            isSeedLaunchPending = false;
            ResetVisualPosition();
        }
    }
}
