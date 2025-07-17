using Nytherion.Core.Managers;
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

        private bool isFacingRight = true;
        private bool isDashing = false;
        private float lastDashTime = -999f;

        private InputManager inputManager;
        private PlayerManager playerManager;

        [Inject]
        public void Construct(InputManager inputManager, PlayerManager playerManager)
        {
            this.inputManager = inputManager;
            this.playerManager = playerManager;
        }

        private void Update()
        {
            if (inputManager == null || playerManager == null) return;
            HandleDashInput();
            HandleSpriteFlip();
        }

        private void FixedUpdate()
        {
            if (inputManager == null) return;
            HandleMovement();
        }

        private void HandleMovement()
        {
            if (isDashing) return;
            Vector2 moveInput = inputManager.MoveInput;
            float currentSpeed = playerManager.currentPlayerData.moveSpeed;
            rb.velocity = moveInput * currentSpeed;
        }

        private void HandleDashInput()
        {
            if (inputManager.Dash && !isDashing && Time.time >= lastDashTime + playerManager.currentPlayerData.dashCooldown)
            {
                StartCoroutine(DashCoroutine());
            }
        }
        private void HandleSpriteFlip()
        {
            Vector2 moveInput = inputManager.MoveInput;
            if (moveInput.x > 0 && !isFacingRight)
            {
                isFacingRight = true;
                spriteRenderer.flipX = false;
            }
            else if (moveInput.x < 0 && isFacingRight)
            {
                isFacingRight = false;
                spriteRenderer.flipX = true;
            }
        }

        private IEnumerator DashCoroutine()
        {
            isDashing = true;
            lastDashTime = Time.time;
            Vector2 moveInput = inputManager.MoveInput;
            Vector2 dashDirection = moveInput.normalized;
            if (dashDirection == Vector2.zero)
            {
                dashDirection = isFacingRight ? Vector2.right : Vector2.left;
            }
            rb.velocity = dashDirection * playerManager.currentPlayerData.dashSpeed;
            yield return new WaitForSeconds(playerManager.currentPlayerData.dashDuration);
            isDashing = false;
        }
    }
}