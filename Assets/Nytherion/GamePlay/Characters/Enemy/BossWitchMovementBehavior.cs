using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody2D))]
    public sealed class BossWitchMovementBehavior : MonoBehaviour,
        IEnemyMovementBehavior, IEnemyFacingBehavior
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float minimumTargetDistance = 6f;
        [SerializeField, Min(0.1f)] private float maximumTargetDistance = 8f;
        [SerializeField] private Vector2 destinationChangeInterval = new Vector2(1.5f, 3f);
        [SerializeField, Min(0.05f)] private float destinationArrivalDistance = 0.35f;
        [SerializeField, Min(0f)] private float facingHorizontalThreshold = 0.2f;

        [Header("Animation")]
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private SpriteRenderer bodyRenderer;

        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
        private static readonly int BrakeState = Animator.StringToHash("Base Layer.Brake");
        private NavMeshAgent agent;
        private Rigidbody2D body;
        private EnemyAIController owner;
        private bool facingRight = true;
        private bool approaching;
        private bool braking;
        private bool combatAnimationLocked;
        private bool castingFacingReversed;
        private bool hasDestination;
        private Vector3 destination;
        private Vector3 destinationTarget;
        private float nextDestinationAt;
        private NavMeshPath candidatePath;

        public bool FacingRight => facingRight;

        public bool RequiresNavMeshAgent => true;
        private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        public Vector3 CurrentVelocity => AgentReady ? agent.desiredVelocity : Vector3.zero;

        private void Awake() => CacheComponents();

        private void CacheComponents()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (owner == null) owner = GetComponent<EnemyAIController>();
            if (bodyAnimator == null) bodyAnimator = GetComponent<Animator>();
            if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
        }

        public void Configure(EnemyData data)
        {
            CacheComponents();
            if (agent == null) return;
            if (data != null) agent.speed = Mathf.Max(0f, data.moveSpeed);
            agent.stoppingDistance = destinationArrivalDistance;
        }

        public void MoveTowards(Vector3 target)
        {
            if (combatAnimationLocked) return;
            if (!AgentReady) return;
            bool arrived = hasDestination && Vector2.Distance(transform.position, destination) <= destinationArrivalDistance;
            bool targetMoved = hasDestination && Vector2.Distance(destinationTarget, target) > 1.5f;
            bool pathBlocked = hasDestination && !agent.pathPending &&
                (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete);
            if (Time.time >= nextDestinationAt || (hasDestination && (arrived || targetMoved || pathBlocked)))
            {
                hasDestination = TryChooseDestination(target);
                float low = Mathf.Max(0.1f, Mathf.Min(destinationChangeInterval.x, destinationChangeInterval.y));
                float high = Mathf.Max(low, Mathf.Max(destinationChangeInterval.x, destinationChangeInterval.y));
                nextDestinationAt = Time.time + (hasDestination ? Random.Range(low, high) : 0.25f);
            }
            if (!hasDestination) { StopMovement(); return; }

            approaching = true;
            braking = false;
            agent.isStopped = false;
            Play(agent.velocity.sqrMagnitude > 0.01f ? RunState : IdleState);
        }

        private bool TryChooseDestination(Vector3 target)
        {
            if (!AgentReady) return false;
            // Nonserialized fields may be cleared by an Editor script reload.
            if (candidatePath == null) candidatePath = new NavMeshPath();
            float near = Mathf.Max(0.1f, minimumTargetDistance);
            float far = Mathf.Max(near, maximumTargetDistance);
            Vector2 away = (Vector2)(transform.position - target);
            float currentDistance = away.magnitude;
            if (away.sqrMagnitude < 0.001f) away = Vector2.right;
            float awayAngle = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            for (int i = 0; i < 16; i++)
            {
                // Choose independent destinations, never advance a fixed orbit angle.
                float angle = (currentDistance < near ? awayAngle + Random.Range(-70f, 70f) :
                    Random.Range(0f, 360f)) * Mathf.Deg2Rad;
                Vector3 point = target + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(near, far);
                point.z = transform.position.z;
                if (!NavMesh.SamplePosition(point, out NavMeshHit sample, 1f, filter)) continue;
                float targetDistance = Vector2.Distance(sample.position, target);
                if (targetDistance < near || targetDistance > far ||
                    Vector2.Distance(sample.position, transform.position) < 1f) continue;
                if (!AgentReady) return false;
                if (!agent.CalculatePath(sample.position, candidatePath) ||
                    candidatePath.status != NavMeshPathStatus.PathComplete) continue;
                // Reject paths that cut through the player to reach the opposite side.
                float clearance = Mathf.Min(currentDistance * 0.9f, near * 0.75f);
                Vector3[] corners = candidatePath.corners;
                bool safe = true;
                Vector3 previous = transform.position;
                foreach (Vector3 corner in corners)
                {
                    if (DistanceToSegment(target, previous, corner) < clearance) { safe = false; break; }
                    previous = corner;
                }
                if (!safe || !agent.SetPath(candidatePath)) continue;
                destination = sample.position;
                destinationTarget = target;
                return true;
            }
            return false;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float t = segment.sqrMagnitude > 0.0001f ?
                Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude) : 0f;
            return Vector2.Distance(point, start + segment * t);
        }

        public void UpdateFacing(Vector2 direction)
        {
            if ((!approaching && !combatAnimationLocked) ||
                Mathf.Abs(direction.x) < facingHorizontalThreshold) return;
            bool wantsRight = direction.x > 0f;
            if (wantsRight == facingRight) return;

            facingRight = wantsRight;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            // FastCast faces the opposite way from the movement artwork.
            if (bodyRenderer != null)
                bodyRenderer.flipX = castingFacingReversed ? facingRight : !facingRight;
        }

        private void PauseAgent()
        {
            if (AgentReady)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (body != null) body.velocity = Vector2.zero;
        }

        private void Play(int state, bool restart = false)
        {
            if (bodyAnimator == null || !bodyAnimator.isActiveAndEnabled ||
                bodyAnimator.layerCount == 0 || !bodyAnimator.HasState(0, state)) return;
            if (restart || bodyAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash != state)
                bodyAnimator.Play(state, 0, 0f);
        }

        public void StopMovement()
        {
            bool wasApproaching = approaching;
            approaching = false;
            hasDestination = false;
            PauseAgent();
            if (combatAnimationLocked)
            {
                braking = false;
                return;
            }
            if (wasApproaching)
            {
                braking = true;
                Play(BrakeState, true);
            }
            else if (!braking)
            {
                Play(IdleState);
            }
        }

        private void LateUpdate()
        {
            if (combatAnimationLocked || !braking) return;
            if (owner != null && !owner.isActiveAndEnabled)
            {
                braking = false;
                return;
            }
            if (bodyAnimator == null || !bodyAnimator.isActiveAndEnabled ||
                bodyAnimator.layerCount == 0) return;

            AnimatorStateInfo state = bodyAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != BrakeState || state.normalizedTime < 1f) return;
            braking = false;
            Play(IdleState);
        }

        public void ResetForReuse()
        {
            CacheComponents();
            combatAnimationLocked = false;
            castingFacingReversed = false;
            hasDestination = false;
            nextDestinationAt = 0f;
            approaching = false;
            braking = false;
            StopMovement();
            facingRight = true;
            ApplyFacing();
        }

        private void OnDisable()
        {
            hasDestination = false;
            nextDestinationAt = 0f;
            combatAnimationLocked = false;
            castingFacingReversed = false;
            approaching = false;
            braking = false;
        }

        public void SetCombatAnimationLocked(bool locked)
        {
            CacheComponents();
            combatAnimationLocked = locked;
            if (locked)
            {
                hasDestination = false;
                nextDestinationAt = 0f;
                approaching = false;
                braking = false;
                PauseAgent();
            }
        }

        public void SetCastingFacingReversed(bool reversed)
        {
            CacheComponents();
            castingFacingReversed = reversed;
            ApplyFacing();
        }
    }
}
