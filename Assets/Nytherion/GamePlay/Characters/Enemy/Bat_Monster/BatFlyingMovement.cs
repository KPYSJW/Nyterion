using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BatFlyingMovement : MonoBehaviour, IEnemyMovementBehavior
    {
        [Header("Flight Path")]
        [Min(0.05f)]
        [SerializeField] private float pathRefreshInterval = 0.25f;
        [Min(0.01f)]
        [SerializeField] private float waypointReachDistance = 0.12f;

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
        private NavMeshPath navMeshPath;
        private Vector3[] pathCorners;
        private int nextCornerIndex;
        private float nextPathRefreshTime;
        private Vector2 lastPlannedDestination;
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

                Vector2 separationPosition = KeepOnBatNavMesh(
                    currentPosition,
                    currentPosition + separationDirection * moveSpeed * Time.fixedDeltaTime);
                rb.MovePosition(separationPosition);
                currentVelocity =
                    (separationPosition - currentPosition) / Time.fixedDeltaTime;
                return;
            }

            if (Time.time >= nextPathRefreshTime ||
                (destination - lastPlannedDestination).sqrMagnitude > 0.25f)
            {
                RefreshPath(currentPosition);
            }

            if (pathCorners == null || nextCornerIndex >= pathCorners.Length)
            {
                currentVelocity = Vector3.zero;
                return;
            }

            while (nextCornerIndex < pathCorners.Length &&
                   ((Vector2)pathCorners[nextCornerIndex] - currentPosition).sqrMagnitude <=
                   waypointReachDistance * waypointReachDistance)
            {
                nextCornerIndex++;
            }

            if (nextCornerIndex >= pathCorners.Length)
            {
                isMoving = false;
                currentVelocity = Vector3.zero;
                return;
            }

            Vector2 targetPosition = pathCorners[nextCornerIndex];
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
            Vector2 nextPosition = KeepOnBatNavMesh(
                currentPosition,
                currentPosition + moveDirection * moveDistance);

            rb.MovePosition(nextPosition);
            currentVelocity = (nextPosition - currentPosition) / Time.fixedDeltaTime;

            if (nextCornerIndex == pathCorners.Length - 1 &&
                (nextPosition - targetPosition).sqrMagnitude <=
                waypointReachDistance * waypointReachDistance)
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
            destination = GetAttackAlignedDestination(target);
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
            nextPathRefreshTime = 0f;
            pathCorners = null;
            navMeshPath?.ClearCorners();
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
            if (navMeshPath == null) navMeshPath = new NavMeshPath();
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

        private NavMeshQueryFilter GetBatNavMeshFilter()
        {
            return new NavMeshQueryFilter
            {
                agentTypeID = navMeshAgent != null ? navMeshAgent.agentTypeID : 0,
                areaMask = NavMesh.AllAreas
            };
        }

        private void RefreshPath(Vector2 currentPosition)
        {
            nextPathRefreshTime = Time.time + pathRefreshInterval;
            lastPlannedDestination = destination;
            pathCorners = null;
            nextCornerIndex = 0;

            NavMeshQueryFilter filter = GetBatNavMeshFilter();
            if (!NavMesh.SamplePosition(currentPosition, out NavMeshHit startHit, 0.75f, filter) ||
                !NavMesh.SamplePosition(destination, out NavMeshHit destinationHit, 1.5f, filter) ||
                !NavMesh.CalculatePath(
                    startHit.position,
                    destinationHit.position,
                    filter,
                    navMeshPath))
            {
                navMeshPath.ClearCorners();
                return;
            }

            pathCorners = navMeshPath.corners;
            nextCornerIndex = pathCorners.Length > 1 ? 1 : pathCorners.Length;
        }

        private Vector2 KeepOnBatNavMesh(Vector2 currentPosition, Vector2 proposedPosition)
        {
            NavMeshQueryFilter filter = GetBatNavMeshFilter();
            if (!NavMesh.SamplePosition(currentPosition, out NavMeshHit currentHit, 0.75f, filter))
                return currentPosition;

            Vector2 meshPosition = currentHit.position;
            if ((meshPosition - currentPosition).sqrMagnitude > 0.01f)
                return meshPosition;

            if (NavMesh.Raycast(meshPosition, proposedPosition, out NavMeshHit boundaryHit, filter))
            {
                float safeDistance = Mathf.Max(0f, boundaryHit.distance - 0.02f);
                proposedPosition = Vector2.MoveTowards(
                    meshPosition,
                    proposedPosition,
                    safeDistance);
            }

            if (!NavMesh.SamplePosition(proposedPosition, out NavMeshHit nextHit, 0.1f, filter))
                return currentPosition;

            return nextHit.position;
        }
    }
}
