using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Player;
using System.Collections;
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
        public float LastDashTime { get; set; } = -999f;

        private InputManager inputManager;
        private PlayerManager playerManager;
        private PlayerState currentState;
        private bool isInitialized = false;


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

            HandleMovement();
        }

        public void HandleMovement()
        {
            if (IsDashing)
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
        private void HandleSpriteFlip()
        {
            Vector2 moveInput = inputManager.MoveInput;
            if (moveInput.x > 0 && !IsFacingRight)
            {
                IsFacingRight = true;
                spriteRenderer.flipX = false;
            }
            else if (moveInput.x < 0 && IsFacingRight)
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

    }
}