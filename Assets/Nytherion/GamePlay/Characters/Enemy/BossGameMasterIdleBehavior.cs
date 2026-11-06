using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody2D), typeof(Animator))]
    [RequireComponent(typeof(BossGameMasterCombatBehavior))]
    public sealed class BossGameMasterIdleBehavior : MonoBehaviour,
        IEnemyMovementBehavior, IEnemyFacingBehavior, IEnemyCombatBehavior
    {
        [Header("Random Drift / 무작위 부유 이동")]
        [SerializeField] private Vector2 directionChangeInterval = new Vector2(1f, 2f);
        [SerializeField, Min(0.1f)] private float minimumTravelDistance = 2f;
        [SerializeField, Min(0.1f)] private float maximumTravelDistance = 4f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.25f;
        [SerializeField, Min(0f)] private float facingHorizontalThreshold = 0.15f;
        [SerializeField] private bool spriteFacesRight = true;
        [Header("Visual Wall Clearance / 이미지 벽 여유 공간")]
        [SerializeField, Min(0f)] private float visualWallPadding = 0.1f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);

        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private BossGameMasterCombatBehavior combat;
        private NavMeshAgent agent;
        private Rigidbody2D body;
        private Animator animator;
        private SpriteRenderer bodyRenderer;
        private NavMeshPath candidatePath;
        private bool directionClockStarted, hasDestination;
        private float nextDirectionAt;
        private Vector3 destination;
        private EnemyBase owner;
        private bool phaseAnimationLocked, attacking;
        private CapsuleCollider2D groundBody;
        private bool controlsAgentPosition;
        private Vector3 navigationToRootOffset;
        private float rootDepth;
        private DungeonManager dungeon;
        private readonly Collider2D[] wallOverlaps = new Collider2D[1];
        private readonly RaycastHit2D[] wallSweeps = new RaycastHit2D[1];
        private bool hasSafePosition;
        private Vector3 safePosition;
        private float nextRecoveryAt;

        public bool RequiresNavMeshAgent => true;
        private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        public Vector3 CurrentVelocity => AgentReady && !agent.isStopped ? agent.desiredVelocity : Vector3.zero;

        private void CacheComponents()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (animator == null) animator = GetComponent<Animator>();
            if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
            if (owner == null) owner = GetComponent<EnemyBase>();
            if (combat == null) combat = GetComponent<BossGameMasterCombatBehavior>();
            if (groundBody == null) groundBody = GetComponent<CapsuleCollider2D>();
            if (dungeon == null) dungeon = FindObjectOfType<DungeonManager>();
        }

        public void PrepareNavigationAlignment()
        {
            CacheComponents();
            hasSafePosition = false;
            nextRecoveryAt = 0f;
            if (agent == null) return;
            // Keep the owning object fixed while binding an agent with a nonzero base offset.
            agent.updatePosition = false;
            controlsAgentPosition = true;
            rootDepth = transform.position.z;
            if (groundBody != null && groundBody.enabled)
            {
                // This is the capsule referenced by EnemyBase's Ground Body Collider on this prefab.
                Bounds bounds = groundBody.bounds;
                agent.radius = Mathf.Max(0.01f, bounds.extents.x);
                agent.height = Mathf.Max(0.02f, bounds.size.y);
                agent.baseOffset = agent.height * 0.5f - (bounds.center.y - transform.position.y);
            }
        }

        public void Configure(EnemyData data)
        {
            CacheComponents();
            PrepareNavigationAlignment();
            if (agent != null)
            {
                agent.speed = data != null ? Mathf.Max(0f, data.moveSpeed) : 2f;
                agent.stoppingDistance = Mathf.Max(0.05f, arrivalDistance);
                if (AgentReady)
                {
                    agent.nextPosition = transform.position;
                    navigationToRootOffset = transform.position - agent.nextPosition;
                }
            }
            StopMovement();
        }

        public void MoveTowards(Vector3 target)
        {
            CacheComponents();
            if (phaseAnimationLocked || attacking) return;
            if (!AgentReady) return;
            if (!EnsureVisualInsideWalls()) return;
            // Only the clock triggers a new choice, independently of the player position.
            if (!directionClockStarted || Time.time >= nextDirectionAt)
            {
                directionClockStarted = true;
                float low = Mathf.Max(0.1f, Mathf.Min(directionChangeInterval.x, directionChangeInterval.y));
                float high = Mathf.Max(low, Mathf.Max(directionChangeInterval.x, directionChangeInterval.y));
                nextDirectionAt = Time.time + Random.Range(low, high);
                hasDestination = TryChooseDestination();
            }
            bool arrived = hasDestination && Vector2.Distance(transform.position, destination) <= arrivalDistance;
            bool blocked = hasDestination && !agent.pathPending &&
                (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete);
            if (!hasDestination || arrived || blocked)
            {
                hasDestination = false;
                StopBody();
            }
            else agent.isStopped = false;
            PlayIdle();
        }

        private bool TryChooseDestination()
        {
            if (!AgentReady) return false;
            // Unity navigation objects must be created on the main thread, including after a script reload.
            if (candidatePath == null) candidatePath = new NavMeshPath();
            float near = Mathf.Max(0.1f, Mathf.Min(minimumTravelDistance, maximumTravelDistance));
            float far = Mathf.Max(near, Mathf.Max(minimumTravelDistance, maximumTravelDistance));
            Vector3 navigationOffset = controlsAgentPosition ? navigationToRootOffset : transform.position - agent.nextPosition;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            for (int attempt = 0; attempt < 24; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 point = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(near, far);
                if (TrySetDestination(point, navigationOffset, filter, near, far)) return true;
            }
            // Try shorter paths around walls, with no preference relative to the player.
            float fallbackAngle = Random.Range(0f, 360f);
            for (int step = 0; step < 3; step++)
            {
                float length = near / (1 << step);
                if (length <= arrivalDistance) break;
                for (int side = 0; side < 16; side++)
                {
                    float angle = (fallbackAngle + side * 22.5f) * Mathf.Deg2Rad;
                    Vector3 point = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * length;
                    if (TrySetDestination(point, navigationOffset, filter, length * 0.9f, far)) return true;
                }
            }
            return false;
        }

        private bool TrySetDestination(Vector3 point, Vector3 navigationOffset,
            NavMeshQueryFilter filter, float near, float far)
        {
            if (!NavMesh.SamplePosition(point - navigationOffset, out NavMeshHit sample, 0.5f, filter)) return false;
            Vector3 rootDestination = sample.position + navigationOffset;
            float distance = Vector2.Distance(rootDestination, transform.position);
            if (distance < near || distance > far || !AgentReady || !VisualPositionIsSafe(rootDestination)) return false;
            if (!agent.CalculatePath(sample.position, candidatePath) || candidatePath.status != NavMeshPathStatus.PathComplete) return false;
            Vector3[] corners = candidatePath.corners;
            if (!PathFitsVisual(corners, navigationOffset) || !agent.SetPath(candidatePath)) return false;
            destination = new Vector3(rootDestination.x, rootDestination.y, transform.position.z);
            return true;
        }

        private void VisualBox(out Vector2 offset, out Vector2 size)
        {
            Bounds bounds = bodyRenderer != null && bodyRenderer.sprite != null ? bodyRenderer.bounds :
                groundBody != null ? groundBody.bounds : new Bounds(transform.position, Vector3.one);
            Vector3 relative = bounds.center - transform.position;
            // Include both flip directions, so turning does not shift a shoulder through the wall.
            offset = new Vector2(0f, relative.y);
            float padding = Mathf.Max(0f, visualWallPadding) * 2f;
            size = new Vector2(Mathf.Max(0.01f, bounds.size.x + Mathf.Abs(relative.x) * 2f) + padding,
                Mathf.Max(0.01f, bounds.size.y) + padding);
        }

        private bool VisualPositionIsSafe(Vector3 rootPosition)
        {
            VisualBox(out Vector2 offset, out Vector2 size);
            Vector2 center = (Vector2)rootPosition + offset;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(wallLayers);
            filter.useTriggers = true;
            if (Physics2D.OverlapBox(center, size, 0f, filter, wallOverlaps) > 0) return false;
            if (owner == null || owner.homeRoom == null || dungeon == null) return true;
            Vector2 min = center - size * 0.5f;
            Vector2 max = center + size * 0.5f;
            for (int x = Mathf.FloorToInt(min.x + 0.001f); x <= Mathf.FloorToInt(max.x - 0.001f); x++)
                for (int y = Mathf.FloorToInt(min.y + 0.001f); y <= Mathf.FloorToInt(max.y - 0.001f); y++)
                    if (dungeon.FindRoomAt(new Vector2Int(x, y)) != owner.homeRoom) return false;
            return true;
        }

        private bool VisualSegmentIsSafe(Vector3 from, Vector3 to)
        {
            if (!VisualPositionIsSafe(to)) return false;
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return true;
            VisualBox(out Vector2 offset, out Vector2 size);
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(wallLayers);
            filter.useTriggers = true;
            return Physics2D.BoxCast((Vector2)from + offset, size, 0f, delta.normalized, filter, wallSweeps, delta.magnitude) == 0;
        }

        private bool PathFitsVisual(Vector3[] corners, Vector3 navigationOffset)
        {
            if (corners == null || corners.Length == 0 || !VisualPositionIsSafe(transform.position)) return false;
            Vector3 previous = transform.position;
            foreach (Vector3 corner in corners)
            {
                Vector3 next = corner + navigationOffset;
                if (!VisualSegmentIsSafe(previous, next)) return false;
                previous = next;
            }
            return true;
        }

        private bool TrySafeRoot(Vector3 candidate, out Vector3 result)
        {
            result = candidate;
            if (!VisualPositionIsSafe(candidate)) return false;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(candidate - navigationToRootOffset, out NavMeshHit sample, 0.5f, filter)) return false;
            result = sample.position + navigationToRootOffset;
            result.z = rootDepth;
            return VisualPositionIsSafe(result);
        }

        private bool EnsureVisualInsideWalls()
        {
            Vector3 current = transform.position;
            if (VisualPositionIsSafe(current))
            {
                safePosition = current;
                hasSafePosition = true;
                nextRecoveryAt = 0f;
                return true;
            }
            hasDestination = false;
            StopBody();
            if (Time.time < nextRecoveryAt) return false;
            nextRecoveryAt = Time.time + 0.5f;
            Vector3 recovered = current;
            bool found = hasSafePosition && TrySafeRoot(safePosition, out recovered);
            VisualBox(out _, out Vector2 size);
            int rings = Mathf.CeilToInt(Mathf.Min(30f, Mathf.Max(size.x, size.y) * 2f) / 0.5f);
            for (int ring = 1; !found && ring <= rings; ring++)
                for (int step = 0; !found && step < 16; step++)
                {
                    float angle = step * Mathf.PI * 2f / 16f;
                    Vector3 candidate = current + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (ring * 0.5f);
                    found = TrySafeRoot(candidate, out recovered);
                }
            if (!found || !agent.Warp(recovered - navigationToRootOffset)) return false;
            ApplyRootPosition(recovered);
            safePosition = recovered;
            hasSafePosition = true;
            nextRecoveryAt = 0f;
            return true;
        }

        private void ApplyRootPosition(Vector3 position)
        {
            position.z = rootDepth;
            transform.position = position;
            if (body != null) body.position = (Vector2)position;
            agent.nextPosition = position - navigationToRootOffset;
        }

        public void UpdateFacing(Vector2 direction)
        {
            if (phaseAnimationLocked || attacking || !hasDestination || !AgentReady || agent.isStopped ||
                Mathf.Abs(direction.x) < facingHorizontalThreshold) return;
            if (bodyRenderer != null) bodyRenderer.flipX = (direction.x > 0f) != spriteFacesRight;
        }

        private void StopBody()
        {
            CacheComponents();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (body != null) body.velocity = Vector2.zero;
        }

        public void StopMovement()
        {
            hasDestination = false;
            directionClockStarted = false;
            StopBody();
            PlayIdle();
        }

        private void PlayIdle()
        {
            if (phaseAnimationLocked || attacking || (owner != null && owner.isDead)) return;
            if (animator != null && animator.isActiveAndEnabled && animator.layerCount > 0 &&
                animator.HasState(0, IdleState) && animator.GetCurrentAnimatorStateInfo(0).fullPathHash != IdleState)
                animator.Play(IdleState, 0, 0f);
        }

        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            CacheComponents();
            if (phaseAnimationLocked || attacking || (AgentReady && !EnsureVisualInsideWalls())) return false;
            return !phaseAnimationLocked && !attacking && combat != null && combat.ShouldEnterAttackState(enemy);
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
            CacheComponents();
            if (combat != null) combat.EnterAttackState(enemy);
            else enemy.TransitionToState(enemy.chaseState);
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            if (phaseAnimationLocked) return;
            combat?.UpdateAttackState(enemy);
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
            combat?.ExitAttackState(enemy);
        }

        public void SetPatternLocked(bool value)
        {
            attacking = value;
            hasDestination = directionClockStarted = false;
            StopBody();
            if (!value) PlayIdle();
        }

        public void FacePattern(Vector2 direction)
        {
            if (bodyRenderer != null && Mathf.Abs(direction.x) > 0.01f)
                bodyRenderer.flipX = (direction.x > 0f) != spriteFacesRight;
        }

        public void SetPhaseAnimationLocked(bool value)
        {
            CacheComponents();
            phaseAnimationLocked = value;
            if (value) combat?.Cancel();
            attacking = false;
            hasDestination = directionClockStarted = false;
            StopBody();
            if (!value)
            {
                combat?.RestartCooldown();
                PlayIdle();
            }
        }

        public void ResetForReuse()
        {
            phaseAnimationLocked = attacking = false;
            hasSafePosition = false;
            nextRecoveryAt = 0f;
            CacheComponents();
            combat?.ResetForReuse();
            nextDirectionAt = 0f;
            StopMovement();
            if (bodyRenderer != null) bodyRenderer.flipX = !spriteFacesRight;
        }

        private void LateUpdate()
        {
            if (!controlsAgentPosition || !AgentReady || owner == null || owner.isDead) return;
            if (!EnsureVisualInsideWalls()) return;
            if (agent.isStopped)
            {
                agent.nextPosition = transform.position - navigationToRootOffset;
                return;
            }
            Vector3 position = agent.nextPosition + navigationToRootOffset;
            position.z = rootDepth;
            if (!VisualSegmentIsSafe(transform.position, position))
            {
                hasDestination = false;
                StopBody();
                agent.nextPosition = transform.position - navigationToRootOffset;
                return;
            }
            ApplyRootPosition(position);
            safePosition = position;
            hasSafePosition = true;
        }

        private void OnDisable()
        {
            combat?.ResetForReuse();
            controlsAgentPosition = false;
            hasSafePosition = false;
            phaseAnimationLocked = attacking = false;
            directionClockStarted = hasDestination = false;
            StopBody();
        }
    }
}
