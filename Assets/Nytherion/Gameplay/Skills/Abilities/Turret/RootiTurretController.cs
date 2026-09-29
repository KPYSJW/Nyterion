using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
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
        private Transform upgradeOwner;
        private float projectileSpreadAngle;
        private float attackAnimationSpeed = 1f;
        private LayerMask projectileObstacleLayers;
        private RootiUpgradeRuntime Upgrades => upgradeOwner != null ? upgradeOwner.GetComponent<RootiUpgradeRuntime>() : null;
        protected override float AttackInterval => base.AttackInterval / (Upgrades != null ? Upgrades.AttackSpeedMultiplier : 1f);
        protected override bool LimitProjectileDistance => false;

        public void SetUpgradeOwner(Transform owner) => upgradeOwner = owner;

        public override void Initialize(TurretSkillData data)
        {
            base.Initialize(data);
            projectileSpreadAngle = data.projectileSpreadAngle;
            projectileObstacleLayers = data.projectileObstacleLayers;
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
                if (elapsed >= attackAnimationDuration / attackAnimationSpeed)
                {
                    LaunchSeed();
                    CompleteAttack();
                }
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
            attackAnimationSpeed = Upgrades != null ? Upgrades.AttackSpeedMultiplier : 1f;
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

        protected override void LaunchProjectileAtTarget(Transform target)
        {
            if (target == null) return;
            int count = 1 + (Upgrades != null ? Upgrades.AdditionalProjectiles : 0);
            // 중앙 씨앗은 조준선을 유지하고 추가 씨앗은 좌우로 펼칩니다.
            Vector2 direction = (target.position - transform.TransformPoint(projectileSpawnOffset)).normalized;
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * projectileSpreadAngle;
                LaunchProjectile(Quaternion.Euler(0f, 0f, angle) * direction);
            }
        }

        protected override void ConfigureProjectile(GameObject projectile)
        {
            if (!projectile.TryGetComponent(out CollisionObject collision)) return;
            // 루티의 씨앗은 플레이어 무기의 관통·튕김과 독립적으로 설정하며 풀 재사용 때도 초기화합니다.
            collision.Configure(damage, null, 0f, null, CombatModifierSnapshot.Empty, true);
            collision.DisableAllProjModifiers();
            collision.ConfigureObstacleCollision(projectileObstacleLayers);
            // 오래된 프리팹이나 이미 생성된 풀에도 거리·화면 이탈 반환이 다시 활성화되지 않게 합니다.
            if (projectile.TryGetComponent(out ProjDistanceLimit distanceLimit)) distanceLimit.enabled = false;
            if (projectile.TryGetComponent(out ProjCameraBoundsLimit cameraLimit)) cameraLimit.enabled = false;
            RootiUpgradeRuntime upgrades = Upgrades;
            BounceModifier bounce = projectile.GetComponent<BounceModifier>();
            bounce.enabled = upgrades != null && upgrades.MaxBounces > 0;
            bounce.Configure(upgrades != null ? upgrades.MaxBounces : 0,
                upgrades != null ? upgrades.BounceRadius : 0f, true, true);
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
            if (animator != null)
            {
                animator.speed = stateName == "Flower Whip Attack" ? attackAnimationSpeed : 1f;
                animator.Play(stateName, 0, 0f);
            }
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
