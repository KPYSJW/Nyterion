using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BatFlyingMovement : MonoBehaviour, IEnemyMovementBehavior
    {
        [Header("Flight Settings")]
        [Min(0f)]
        [SerializeField] private float roomBoundaryPadding = 0.75f;

        [Header("Flying Enemy Separation")]
        [Min(0.01f)]
        [SerializeField] private float separationRadius = 1.2f;
        [Range(0f, 0.95f)]
        [SerializeField] private float separationStrength = 0.75f;

        [Header("Attack Approach")]
        [Tooltip("이 공격 원의 중심이 추적 대상에게 접근하도록 이동 목적지를 보정합니다.")]
        [SerializeField] private CircleCollider2D attackRangeCollider;

        private static readonly Collider2D[] SeparationHits = new Collider2D[24];

        private Rigidbody2D rb;
        private NavMeshAgent navMeshAgent;
        private EnemyBase enemyBase;
        private Vector2 destination;
        private Vector3 currentVelocity;
        private float moveSpeed;
        private bool isMoving;

        public bool RequiresNavMeshAgent => false;
        public Vector3 CurrentVelocity => currentVelocity;

        private void Awake()
        {
            CacheReferences();
        }

        private void FixedUpdate()
        {
            if (rb == null || moveSpeed <= 0f)
            {
                currentVelocity = Vector3.zero;
                return;
            }

            Vector2 currentPosition = rb.position;
            Vector2 separationDirection = CalculateSeparationDirection(currentPosition);

            if (!isMoving)
            {
                if (separationDirection.sqrMagnitude <= 0.001f)
                {
                    currentVelocity = Vector3.zero;
                    return;
                }

                Vector2 separationPosition = ClampToHomeRoom(
                    currentPosition + separationDirection * moveSpeed * Time.fixedDeltaTime);
                rb.MovePosition(separationPosition);
                currentVelocity =
                    (separationPosition - currentPosition) / Time.fixedDeltaTime;
                return;
            }

            Vector2 targetPosition = ClampToHomeRoom(destination);
            Vector2 toTarget = targetPosition - currentPosition;

            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                isMoving = false;
                currentVelocity = Vector3.zero;
                return;
            }

            Vector2 chaseDirection = toTarget.normalized;
            Vector2 moveDirection = (
                chaseDirection + separationDirection * separationStrength).normalized;
            float moveDistance = Mathf.Min(
                moveSpeed * Time.fixedDeltaTime,
                toTarget.magnitude);
            Vector2 nextPosition = ClampToHomeRoom(
                currentPosition + moveDirection * moveDistance);

            rb.MovePosition(nextPosition);
            currentVelocity = (nextPosition - currentPosition) / Time.fixedDeltaTime;

            if ((nextPosition - targetPosition).sqrMagnitude <= 0.0001f)
            {
                isMoving = false;
                currentVelocity = Vector3.zero;
            }
        }

        public void Configure(EnemyData data)
        {
            CacheReferences();
            moveSpeed = data != null ? data.moveSpeed : 0f;
            DisableGroundNavigation();
        }

        public void MoveTowards(Vector3 target)
        {
            destination = ClampToHomeRoom(GetAttackAlignedDestination(target));
            isMoving = true;
        }

        public void StopMovement()
        {
            isMoving = false;
            currentVelocity = Vector3.zero;

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        public void ResetForReuse()
        {
            CacheReferences();
            StopMovement();
            DisableGroundNavigation();
        }

        private void OnDisable()
        {
            StopMovement();
        }

        private void CacheReferences()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
            if (enemyBase == null) enemyBase = GetComponent<EnemyBase>();
        }

        private void DisableGroundNavigation()
        {
            if (navMeshAgent == null || !navMeshAgent.enabled) return;

            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
            }

            navMeshAgent.enabled = false;
        }

        private Vector2 CalculateSeparationDirection(Vector2 currentPosition)
        {
            if (separationRadius <= 0f)
                return Vector2.zero;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int layerMask = enemyLayer >= 0 ? 1 << enemyLayer : Physics2D.AllLayers;
            int hitCount = Physics2D.OverlapCircleNonAlloc(
                currentPosition,
                separationRadius,
                SeparationHits,
                layerMask);

            Vector2 separation = Vector2.zero;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = SeparationHits[i];
                if (hit == null) continue;

                BatFlyingMovement other = hit.GetComponentInParent<BatFlyingMovement>();
                if (other == null || other == this || !other.isActiveAndEnabled)
                    continue;

                Vector2 away = currentPosition - (Vector2)other.transform.position;
                float distance = away.magnitude;

                if (distance <= 0.001f)
                {
                    away = GetDeterministicOverlapDirection(other);
                    distance = 0f;
                }
                else
                {
                    away /= distance;
                }

                float proximity = 1f - Mathf.Clamp01(distance / separationRadius);
                separation += away * proximity;
            }

            return Vector2.ClampMagnitude(separation, 1f);
        }

        private Vector2 GetAttackAlignedDestination(Vector2 target)
        {
            if (attackRangeCollider == null)
                return target;

            Vector2 rootPosition = rb != null
                ? rb.position
                : (Vector2)transform.position;
            Vector2 rangeCenter = attackRangeCollider.transform.TransformPoint(
                attackRangeCollider.offset);
            Vector2 rangeOffset = rangeCenter - rootPosition;

            return target - rangeOffset;
        }

        private Vector2 GetDeterministicOverlapDirection(BatFlyingMovement other)
        {
            int myId = GetInstanceID();
            int otherId = other.GetInstanceID();
            int lowerId = Mathf.Min(myId, otherId);
            int upperId = Mathf.Max(myId, otherId);
            int hash = unchecked(lowerId * 397 ^ upperId);
            float angle = (hash & 1023) * (360f / 1024f) * Mathf.Deg2Rad;
            Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            return myId < otherId ? axis : -axis;
        }

        private Vector2 ClampToHomeRoom(Vector2 target)
        {
            if (enemyBase == null || enemyBase.homeRoom == null)
                return target;

            BoundsInt roomBounds = enemyBase.homeRoom.Bounds;
            float minX = roomBounds.xMin + roomBoundaryPadding;
            float maxX = roomBounds.xMax - roomBoundaryPadding;
            float minY = roomBounds.yMin + roomBoundaryPadding;
            float maxY = roomBounds.yMax - roomBoundaryPadding;

            if (minX > maxX) minX = maxX = roomBounds.center.x;
            if (minY > maxY) minY = maxY = roomBounds.center.y;

            return new Vector2(
                Mathf.Clamp(target.x, minX, maxX),
                Mathf.Clamp(target.y, minY, maxY));
        }
    }
}
