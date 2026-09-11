using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class Knight : MonoBehaviour, IEnemyMovementBehavior
    {
        [Header("Speed")]
        [SerializeField] private float MaxSpeed = 7f;
        [SerializeField] private float SpeedGainPerSecond = 1.3f;
        [SerializeField] private float SpeedLossPerSecond = 2f;

        [Header("Turning")]
        [SerializeField] private float NavAcceleration = 3f;
        [SerializeField] private float AngularSpeed = 80f;
        [SerializeField, Range(-1f, 1f)]
        private float stableDirectionDot = 0.9f;

        [Header("Player Knockback")]
        [SerializeField] private float KnockbackMultiplier = 1.5f;
        [SerializeField] private float MinimumKnockback = 3f;
        [SerializeField] private float MaximumKnockback = 10f;
        [SerializeField] private float KnockbackDuration = 0.25f;
        [SerializeField] private float BackwardWeight = 0.75f;
        [SerializeField] private float SideWeight = 0.65f;
        [SerializeField] private float KnockbackCooldown = 0.5f;

        private NavMeshAgent agent;
        private float baseSpeed;
        private float currentSpeed;
        private float stableMoveTime;
        private Vector2 lastMoveDirection;
        private float lastMoveSpeed;
        private float nextKnockbackTime;
        private EnemyBase enemyBase;
        private StatusEffectManager statusEffectManager;

        public bool RequiresNavMeshAgent => true;
        public float CurrentSpeed => agent != null
            ? agent.velocity.magnitude
            : 0f;

        public Vector3 CurrentVelocity => agent != null
            ? agent.velocity
            : Vector3.zero;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            enemyBase = GetComponent<EnemyBase>();
            statusEffectManager = GetComponent<StatusEffectManager>();
        }

        public void Configure(EnemyData data)
        {
            if (data == null || agent == null)
                return;

            if (enemyBase == null)
            {
                enemyBase = GetComponent<EnemyBase>();
            }

            if (statusEffectManager == null)
            {
                statusEffectManager = GetComponent<StatusEffectManager>();
            }

            baseSpeed = data.moveSpeed;
            ResetMovement();

            agent.acceleration = NavAcceleration;
            agent.angularSpeed = AngularSpeed;
            agent.autoBraking = false;
            agent.stoppingDistance = 0f;
        }

        public void MoveTowards(Vector3 destination)
        {
            if (agent == null || !agent.isOnNavMesh)
                return;

            agent.isStopped = false;
            agent.SetDestination(destination);

            Vector3 desired = agent.desiredVelocity;
            Vector3 actual = agent.velocity;

            Vector2 actualVelocity = new Vector2(actual.x, actual.y);
            if (actualVelocity.sqrMagnitude > 0.01f)
            {
                lastMoveDirection = actualVelocity.normalized;
                lastMoveSpeed = actualVelocity.magnitude;
            }

            bool isStableDirection = true;

            if (desired.sqrMagnitude > 0.01f &&
                actual.sqrMagnitude > 0.01f)
            {
                float alignment = Vector3.Dot(
                    desired.normalized,
                    actual.normalized);

                isStableDirection = alignment >= stableDirectionDot;
            }

            if (isStableDirection)
            {
                stableMoveTime += Time.deltaTime;

                float targetSpeed =
                    baseSpeed + stableMoveTime * SpeedGainPerSecond;

                currentSpeed = Mathf.Min(targetSpeed, MaxSpeed);
            }
            else
            {
                stableMoveTime = Mathf.Max(
                    0f,
                    stableMoveTime - Time.deltaTime * SpeedLossPerSecond);

                currentSpeed = Mathf.MoveTowards(
                    currentSpeed,
                    baseSpeed,
                    SpeedLossPerSecond * Time.deltaTime);
            }

            agent.speed = currentSpeed;
        }

        public void StopMovement()
        {
            stableMoveTime = 0f;
            currentSpeed = baseSpeed;

            if (agent == null ||
                !agent.isActiveAndEnabled ||
                !agent.isOnNavMesh)
            {
                return;
            }

            agent.speed = baseSpeed;
            agent.isStopped = true;
            agent.ResetPath();
        }

        public void ResetMovement()
        {
            stableMoveTime = 0f;
            currentSpeed = baseSpeed;
            lastMoveDirection = Vector2.zero;
            lastMoveSpeed = 0f;
            nextKnockbackTime = 0f;

            if (agent != null)
            {
                agent.speed = baseSpeed;
            }
        }

        public void ResetForReuse()
        {
            ResetMovement();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (Time.time < nextKnockbackTime)
                return;

            PlayerController playerController =
                collision.collider.GetComponentInParent<PlayerController>();

            if (playerController == null)
                return;

            if (playerController.IsDashing)
                return;

            Vector2 moveDirection = lastMoveDirection;
            if (moveDirection.sqrMagnitude <= 0.01f)
            {
                moveDirection = ((Vector2)playerController.transform.position -
                    (Vector2)transform.position).normalized;
            }

            if (moveDirection.sqrMagnitude <= 0.01f)
                return;

            Vector2 toPlayer = ((Vector2)playerController.transform.position -
                (Vector2)transform.position).normalized;

            float cross = moveDirection.x * toPlayer.y -
                moveDirection.y * toPlayer.x;

            float sideSign;
            if (Mathf.Abs(cross) > 0.001f)
            {
                sideSign = Mathf.Sign(cross);
            }
            else
            {
                sideSign = GetInstanceID() % 2 == 0 ? 1f : -1f;
            }

            Vector2 backward = -moveDirection;
            Vector2 side = new Vector2(-moveDirection.y, moveDirection.x) * sideSign;
            Vector2 knockbackDirection =
                (backward * BackwardWeight + side * SideWeight).normalized;

            float impactSpeed = Mathf.Max(lastMoveSpeed, CurrentSpeed);
            float knockbackSpeed = Mathf.Clamp(
                impactSpeed * KnockbackMultiplier,
                MinimumKnockback,
                MaximumKnockback);

            PlayerHealth playerHealth =
                collision.collider.GetComponentInParent<PlayerHealth>();

            float damage = GetCollisionDamage();
            if (playerHealth != null && damage > 0f)
            {
                playerHealth.TakeDamage(damage);
            }

            playerController.ApplyKnockback(
                knockbackDirection,
                knockbackSpeed,
                KnockbackDuration);

            nextKnockbackTime = Time.time + KnockbackCooldown;
        }

        private float GetCollisionDamage()
        {
            if (enemyBase == null || enemyBase.enemyData == null)
                return 0f;

            float damage = enemyBase.enemyData.damageAmount;
            if (statusEffectManager != null)
            {
                damage *= statusEffectManager.GetOutgoingDamageMultiplier();
            }

            return damage;
        }
    }
}
