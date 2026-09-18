using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;

        public Vector2 MoveInput
        {
            get
            {
                if (inputManager == null)
                {
                    Debug.LogWarning("[PlayerController] MoveInput - inputManager is null!");
                    return Vector2.zero;
                }
                return inputManager.MoveInput;
            }
        }
        public bool IsDashPressed => inputManager?.Dash ?? false;
        public PlayerData PlayerData => playerManager?.currentPlayerData;

        public bool IsFacingRight { get; private set; } = true;
        public bool IsDashing { get; set; } = false;
        public bool IsKnockedBack { get; private set; } = false;
        public float LastDashTime { get; set; } = -999f;

        private InputManager inputManager;
        private PlayerManager playerManager;
        private PlayerState currentState;
        private bool isInitialized = false;
        private Vector2 knockbackVelocity;
        private float knockbackDuration;
        private float knockbackTimeRemaining;
        private readonly List<DashColliderState> dashColliderStates = new();
        private readonly Collider2D[] dashOverlapResults = new Collider2D[64];
        private Coroutine dashCollisionRestoreCoroutine;
        private PlayerHealth playerHealth;
        private bool dashProtectionActive;
        private bool wasInvulnerableBeforeDash;
        private LayerMask dashEnemyCollisionMask;

        private readonly struct DashColliderState
        {
            public readonly Collider2D Collider;
            public readonly int OriginalExcludeLayers;

            public DashColliderState(Collider2D collider, int originalExcludeLayers)
            {
                Collider = collider;
                OriginalExcludeLayers = originalExcludeLayers;
            }
        }


        public void Construct(InputManager inputManager, PlayerManager playerManager)
        {
            this.inputManager = inputManager;
            this.playerManager = playerManager;
        }

        private void Start()
        {
            StartCoroutine(InitializeWhenReady());
        }

        private IEnumerator InitializeWhenReady()
        {

            int waitCount = 0;
            while (inputManager == null || playerManager == null)
            {
                waitCount++;

                yield return null;
            }

            waitCount = 0;
            while (playerManager.currentPlayerData == null)
            {
                waitCount++;

                yield return null;
            }

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
                if (rb == null)
                {
                    yield break;
                }
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }


            ChangeState(new IdleState());
            isInitialized = true;
        }
        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            if (inputManager == null || playerManager == null || currentState == null)
            {
                return;
            }

            Vector2 moveInput = MoveInput;

            if (IsKnockedBack)
            {
                HandleSpriteFlip();
                return;
            }

            currentState.Execute(this);
            HandleSpriteFlip();
        }

        private void FixedUpdate()
        {
            if (!isInitialized)
            {
                return;
            }

            if (inputManager == null)
            {
                return;
            }

            if (IsKnockedBack)
            {
                UpdateKnockback();
                return;
            }

            HandleMovement();
        }

        public void HandleMovement()
        {
            if (IsDashing || IsKnockedBack)
            {
                return;
            }

            if (rb == null)
            {
                return;
            }

            Vector2 moveInput = MoveInput;
            if (moveInput.magnitude > 0.1f) // 입력이 있을 때만 로그
            {
                Vector2 finalVelocity = moveInput * PlayerData.moveSpeed;

                rb.velocity = finalVelocity;

            }
            else
            {
                rb.velocity = Vector2.zero;
            }
        }

        public void ApplyKnockback(Vector2 direction, float speed, float duration)
        {
            if (rb == null || direction.sqrMagnitude <= 0.001f || speed <= 0f)
            {
                return;
            }

            if (IsDashing)
            {
               return;
            }

            knockbackDuration = Mathf.Max(0.01f, duration);
            knockbackTimeRemaining = knockbackDuration;
            knockbackVelocity = direction.normalized * speed;
            IsKnockedBack = true;
            rb.velocity = knockbackVelocity;
        }

        private void UpdateKnockback()
        {
            if (rb == null)
            {
                IsKnockedBack = false;
                return;
            }

            knockbackTimeRemaining -= Time.fixedDeltaTime;

            if (knockbackTimeRemaining <= 0f)
            {
                IsKnockedBack = false;
                knockbackVelocity = Vector2.zero;
                rb.velocity = Vector2.zero;
                return;
            }

            float remainingRatio = knockbackTimeRemaining / knockbackDuration;
            rb.velocity = knockbackVelocity * remainingRatio;
        }
        private void HandleSpriteFlip()
        {
            if (Camera.main == null || inputManager == null) return;

            Vector2 mouseScreenPos = inputManager.MousePosition;
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));

            if (mouseWorldPos.x > transform.position.x && !IsFacingRight)
            {
                IsFacingRight = true;
                spriteRenderer.flipX = false;
            }
            else if (mouseWorldPos.x < transform.position.x && IsFacingRight)
            {
                IsFacingRight = false;
                spriteRenderer.flipX = true;
            }
        }
        public void ApplyDashVelocity()
        {
            Vector2 dashDirection = MoveInput.normalized;
            if (dashDirection == Vector2.zero)
            {
                dashDirection = IsFacingRight ? Vector2.right : Vector2.left;
            }
            rb.velocity = dashDirection * PlayerData.dashSpeed;
        }

        public void BeginDashProtection()
        {
            if (!dashProtectionActive)
            {
                playerHealth ??= GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    wasInvulnerableBeforeDash = playerHealth.IsInvulnerable;
                    playerHealth.SetInvulnerable(true);
                }

                dashProtectionActive = true;
            }

            if (dashColliderStates.Count > 0)
                return;

            dashEnemyCollisionMask = LayerMask.GetMask("Enemy", "FlyingBody");
            Collider2D[] playerColliders = GetComponents<Collider2D>();
            foreach (Collider2D playerCollider in playerColliders)
            {
                if (playerCollider == null || playerCollider.isTrigger)
                    continue;

                int originalExcludeLayers = playerCollider.excludeLayers;
                dashColliderStates.Add(
                    new DashColliderState(playerCollider, originalExcludeLayers));
                playerCollider.excludeLayers =
                    originalExcludeLayers | dashEnemyCollisionMask.value;
            }
        }

        public void EndDashProtection()
        {
            if (dashProtectionActive)
            {
                playerHealth ??= GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.SetInvulnerable(wasInvulnerableBeforeDash);
                }

                dashProtectionActive = false;
            }

            RestoreSeparatedDashCollisions();
            if (dashColliderStates.Count > 0 && dashCollisionRestoreCoroutine == null)
            {
                dashCollisionRestoreCoroutine =
                    StartCoroutine(RestoreDashCollisionsWhenSeparated());
            }
        }

        private IEnumerator RestoreDashCollisionsWhenSeparated()
        {
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

            while (dashColliderStates.Count > 0)
            {
                yield return waitForFixedUpdate;

                if (dashProtectionActive)
                    continue;

                RestoreSeparatedDashCollisions();
            }

            dashCollisionRestoreCoroutine = null;
        }

        private void RestoreSeparatedDashCollisions()
        {
            if (dashProtectionActive || IsOverlappingDashEnemy())
                return;

            RestoreAllDashCollisions();
        }

        private bool IsOverlappingDashEnemy()
        {
            ContactFilter2D contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(dashEnemyCollisionMask);
            contactFilter.useTriggers = false;

            foreach (DashColliderState state in dashColliderStates)
            {
                Collider2D playerCollider = state.Collider;
                if (playerCollider == null || !playerCollider.isActiveAndEnabled)
                    continue;

                if (playerCollider.OverlapCollider(
                        contactFilter,
                        dashOverlapResults) > 0)
                    return true;
            }

            return false;
        }

        private void RestoreAllDashCollisions()
        {
            foreach (DashColliderState state in dashColliderStates)
            {
                if (state.Collider != null)
                {
                    state.Collider.excludeLayers = state.OriginalExcludeLayers;
                }
            }

            dashColliderStates.Clear();
            dashCollisionRestoreCoroutine = null;
        }

        public void NotifyDashStarted()
        {
            playerManager?.EventManager?.TriggerPlayerDashStarted();
        }

        public void ChangeState(PlayerState newState)
        {
            currentState?.Exit(this);

            currentState = newState;
            currentState.Enter(this);
        }
        public void PlayAnimation(string animationName)
        {
            animator.Play(animationName);
        }

        public void HandleSkillInput(int index)
        {
            
        }

        private void OnDisable()
        {
            if (dashProtectionActive)
            {
                playerHealth ??= GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.SetInvulnerable(wasInvulnerableBeforeDash);
                }

                dashProtectionActive = false;
            }

            RestoreAllDashCollisions();
        }

    }
}
