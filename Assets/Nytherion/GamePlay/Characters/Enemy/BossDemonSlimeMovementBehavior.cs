using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody2D))]
    public sealed class BossDemonSlimeMovementBehavior : MonoBehaviour,
        IEnemyMovementBehavior, IEnemyFacingBehavior
    {
        [SerializeField, Min(0f)] private float stoppingDistance = 1.6f;
        [SerializeField, Min(0f)] private float resumeDistancePadding = 0.25f;
        [SerializeField] private bool spriteFacesRight = false;
        [SerializeField] private bool demonSpriteFacesRight = false;
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private SpriteRenderer bodyRenderer;

        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
        private NavMeshAgent agent;
        private Rigidbody2D body;
        private bool approaching;
        private bool facingRight = true;
        private bool demonForm;
        private bool phaseAnimationLocked;

        public bool FacingRight => facingRight;

        public bool RequiresNavMeshAgent => true;
        private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        public Vector3 CurrentVelocity => AgentReady ? agent.desiredVelocity : Vector3.zero;

        private void Awake() => CacheComponents();

        private void CacheComponents()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyAnimator == null) bodyAnimator = GetComponent<Animator>();
            if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
        }

        public void Configure(EnemyData data)
        {
            CacheComponents();
            if (agent == null) return;
            if (data != null) agent.speed = Mathf.Max(0f, data.moveSpeed);
            agent.stoppingDistance = stoppingDistance;
        }

        public void MoveTowards(Vector3 destination)
        {
            if (phaseAnimationLocked) return;
            if (!AgentReady) return;
            float arrivalDistance = stoppingDistance + (approaching ? 0f : resumeDistancePadding);
            if (Vector2.Distance(transform.position, destination) <= arrivalDistance)
            {
                StopMovement();
                return;
            }

            approaching = true;
            agent.isStopped = false;
            agent.SetDestination(destination);
            Play(agent.velocity.sqrMagnitude > 0.01f ? RunState : IdleState);
        }

        public void UpdateFacing(Vector2 direction)
        {
            if (phaseAnimationLocked || !approaching || Mathf.Abs(direction.x) < 0.1f) return;
            facingRight = direction.x > 0f;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            bool sourceFacesRight = demonForm ? demonSpriteFacesRight : spriteFacesRight;
            if (bodyRenderer != null) bodyRenderer.flipX = facingRight != sourceFacesRight;
        }

        public void StopMovement()
        {
            approaching = false;
            if (AgentReady)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (body != null) body.velocity = Vector2.zero;
            if (!phaseAnimationLocked) Play(IdleState);
        }

        public void SetDemonForm(bool value)
        {
            CacheComponents();
            demonForm = value;
            ApplyFacing();
        }

        public void SetPhaseAnimationLocked(bool value)
        {
            CacheComponents();
            phaseAnimationLocked = value;
            if (value) StopMovement();
        }

        public void FaceAttackDirection(Vector2 direction)
        {
            CacheComponents();
            if (Mathf.Abs(direction.x) > 0.01f) facingRight = direction.x > 0f;
            ApplyFacing();
        }

        private void Play(int state)
        {
            if (bodyAnimator == null || !bodyAnimator.isActiveAndEnabled ||
                bodyAnimator.layerCount == 0 || !bodyAnimator.HasState(0, state)) return;
            if (bodyAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash != state)
                bodyAnimator.Play(state, 0, 0f);
        }

        public void ResetForReuse()
        {
            CacheComponents();
            phaseAnimationLocked = false;
            demonForm = false;
            StopMovement();
            facingRight = true;
            ApplyFacing();
        }

        private void OnDisable()
        {
            approaching = false;
            phaseAnimationLocked = false;
            if (body != null) body.velocity = Vector2.zero;
        }
    }
}
