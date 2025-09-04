using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Player;
using System.Collections;
using UnityEngine;
using Zenject;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;

        public Vector2 MoveInput => inputManager.MoveInput;
        public bool IsDashPressed => inputManager.Dash;
        public PlayerData PlayerData => playerManager.currentPlayerData;

        public bool IsFacingRight { get; private set; } = true;
        public bool IsDashing { get; set; } = false;
        public float LastDashTime { get; set; } = -999f;

        private InputManager inputManager;
        private PlayerManager playerManager;
        private PlayerState currentState;


        [Inject]
        public void Construct(InputManager inputManager, PlayerManager playerManager)
        {
            this.inputManager = inputManager;
            this.playerManager = playerManager;
        }
        private void Start()
        {
            ChangeState(new IdleState());
        }
        private void Update()
        {
            if (inputManager == null || playerManager == null || currentState == null) return;

            currentState.Execute(this);
            HandleSpriteFlip();
        }

        private void FixedUpdate()
        {
            if (inputManager == null) return;
            HandleMovement();
        }

        public void HandleMovement()
        {
            if (IsDashing) return;
            rb.velocity = MoveInput * PlayerData.moveSpeed;
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

    }
}