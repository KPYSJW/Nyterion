using System.Collections;
using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class TreeRandomRangedBehavior : MonoBehaviour,
        IEnemyMovementBehavior, IEnemyCombatBehavior
    {
        [Header("Random Movement")]
        [Min(0.1f)] [SerializeField] private float fallbackMoveSpeed = 2.5f;
        [Min(0.5f)] [SerializeField] private float minWanderDistance = 2f;
        [Min(0.5f)] [SerializeField] private float maxWanderDistance = 6f;
        [Min(0.1f)] [SerializeField] private float destinationInterval = 2f;
        [Min(0f)] [SerializeField] private float roomBoundaryPadding = 0.75f;

        [Header("Random Projectile")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [Min(0.1f)] [SerializeField] private float minFireInterval = 1.75f;
        [Min(0.1f)] [SerializeField] private float maxFireInterval = 3.25f;
        [Min(0.1f)] [SerializeField] private float projectileSpeed = 8f;
        [Min(0f)] [SerializeField] private float fallbackDamage = 5f;

        [Header("Volley")]
        [Min(1)] [SerializeField] private int projectileCount = 3;

        [Header("Fire Effect")]
        [SerializeField] private GameObject fireEffect;
        [SerializeField] private Animator fireEffectAnimator;
        [Min(0f)] [SerializeField] private float projectileSpawnDelay = 0.225f;

        private EnemyBase enemyBase;
        private EnemyAIController aiController;
        private NavMeshAgent agent;
        private Animator animator;
        private float moveSpeed;
        private float destinationTimer;
        private float fireTimer;
        private bool movementEnabled;
        private Coroutine fireSequence;

        public bool RequiresNavMeshAgent => true;
        public Vector3 CurrentVelocity =>
            agent != null && agent.enabled ? agent.desiredVelocity : Vector3.zero;

        private void Awake()
        {
            CacheReferences();
            ResetTimers();
        }

        private void OnEnable()
        {
            movementEnabled = true;
            ResetTimers();
        }

        private void Update()
        {
            if (enemyBase != null && enemyBase.isDead) return;

            KeepRunAnimationPlaying();
            UpdateRandomMovement();
            UpdateRandomFire();
        }

        public void Configure(EnemyData data)
        {
            CacheReferences();
            moveSpeed = data != null && data.moveSpeed > 0f
                ? data.moveSpeed
                : fallbackMoveSpeed;

            if (agent != null)
            {
                agent.speed = moveSpeed;
                agent.updateRotation = false;
                agent.updateUpAxis = false;
            }
        }

        // EnemyAI가 플레이어 위치를 넘겨도 사용하지 않는다.
        // 이 적은 항상 자신이 뽑은 무작위 목적지만 향한다.
        public void MoveTowards(Vector3 destination)
        {
            movementEnabled = true;
            EnsureWanderDestination();
        }

        public void StopMovement()
        {
            movementEnabled = false;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        public void ResetForReuse()
        {
            CacheReferences();
            StopFireEffect();
            movementEnabled = true;
            ResetTimers();
        }

        private void OnDisable()
        {
            StopFireEffect();
            StopMovement();
        }

        private void UpdateRandomMovement()
        {
            if (!movementEnabled || agent == null || !agent.enabled ||
                !agent.isOnNavMesh || moveSpeed <= 0f)
                return;

            destinationTimer -= Time.deltaTime;
            bool arrived = !agent.pathPending && agent.hasPath &&
                           agent.remainingDistance <= agent.stoppingDistance + 0.15f;

            if (destinationTimer <= 0f || arrived || !agent.hasPath)
            {
                PickNewWanderDestination();
            }
        }

        private void EnsureWanderDestination()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = false;
            if (!agent.hasPath) PickNewWanderDestination();
        }

        private void PickNewWanderDestination()
        {
            float minDistance = Mathf.Min(minWanderDistance, maxWanderDistance);
            float maxDistance = Mathf.Max(minWanderDistance, maxWanderDistance);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;

                Vector2 candidate = (Vector2)transform.position + direction *
                    Random.Range(minDistance, maxDistance);
                candidate = ClampToHomeRoom(candidate);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f,
                        agent.areaMask))
                    continue;

                agent.isStopped = false;
                agent.SetDestination(hit.position);
                destinationTimer = destinationInterval;
                return;
            }

            destinationTimer = 0.25f;
        }

        private void UpdateRandomFire()
        {
            if (fireSequence != null) return;

            fireTimer -= Time.deltaTime;
            if (fireTimer > 0f) return;

            if (CanPlayFireEffect())
                fireSequence = StartCoroutine(PlayFireSequence());
            else
                FireVolley();

            ScheduleNextShot();
        }

        private IEnumerator PlayFireSequence()
        {
            fireEffect.SetActive(true);
            fireEffectAnimator.Play("Effect", 0, 0f);
            yield return new WaitForSeconds(projectileSpawnDelay);

            if (enemyBase == null || !enemyBase.isDead)
                FireVolley();

            fireSequence = null;
        }

        private void FireVolley()
        {
            if (projectilePrefab == null) return;

            int count = Mathf.Max(1, projectileCount);
            for (int i = 0; i < count; i++)
            {
                SpawnProjectile(GetRandomDirection());
            }
        }

        private void SpawnProjectile(Vector2 direction)
        {
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject projectile = Instantiate(
                projectilePrefab, origin, Quaternion.AngleAxis(angle, Vector3.forward));

            if (projectile.TryGetComponent(out EnemyProjectiles enemyProjectile))
            {
                enemyProjectile.Initialize(GetDamage());
            }

            if (projectile.TryGetComponent(out Rigidbody2D projectileBody))
            {
                projectileBody.velocity = direction * projectileSpeed;
            }
        }

        private static Vector2 GetRandomDirection()
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            return direction;
        }

        private bool CanPlayFireEffect()
        {
            return fireEffect != null && fireEffectAnimator != null;
        }

        private void StopFireEffect()
        {
            if (fireSequence != null)
            {
                StopCoroutine(fireSequence);
                fireSequence = null;
            }

            fireEffect?.SetActive(false);
        }

        private float GetDamage()
        {
            if (enemyBase != null && enemyBase.enemyData != null &&
                enemyBase.enemyData.damageAmount > 0f)
                return enemyBase.enemyData.damageAmount;

            return fallbackDamage;
        }

        private Vector2 ClampToHomeRoom(Vector2 target)
        {
            if (enemyBase == null || enemyBase.homeRoom == null) return target;

            var bounds = enemyBase.homeRoom.Bounds;
            float minX = bounds.xMin + roomBoundaryPadding;
            float maxX = bounds.xMax - roomBoundaryPadding;
            float minY = bounds.yMin + roomBoundaryPadding;
            float maxY = bounds.yMax - roomBoundaryPadding;

            if (minX > maxX) minX = maxX = bounds.center.x;
            if (minY > maxY) minY = maxY = bounds.center.y;

            return new Vector2(
                Mathf.Clamp(target.x, minX, maxX),
                Mathf.Clamp(target.y, minY, maxY));
        }

        private void KeepRunAnimationPlaying()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Run"))
                animator.Play("Run");
        }

        private void ResetTimers()
        {
            destinationTimer = 0f;
            ScheduleNextShot();
        }

        private void ScheduleNextShot()
        {
            float minInterval = Mathf.Min(minFireInterval, maxFireInterval);
            float maxInterval = Mathf.Max(minFireInterval, maxFireInterval);
            fireTimer = Random.Range(minInterval, maxInterval);
        }

        private void CacheReferences()
        {
            if (enemyBase == null) enemyBase = GetComponent<EnemyBase>();
            if (aiController == null) aiController = GetComponent<EnemyAIController>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponent<Animator>();
        }

        public bool ShouldEnterAttackState(EnemyAIController enemy) => false;
        public void EnterAttackState(EnemyAIController enemy) { }
        public void UpdateAttackState(EnemyAIController enemy) { }
        public void ExitAttackState(EnemyAIController enemy) { }
    }
}
