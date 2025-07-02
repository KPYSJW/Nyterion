using Nytherion.Core.Managers;
using System.Collections;
using UnityEngine;

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

        private void Update()
        {
            if (InputManager.Instance == null) return;
            HandleDashInput();
            HandleSpriteFlip();
        }

        private void FixedUpdate()
        {
            if (InputManager.Instance == null) return;
            HandleMovement();
        }

        private void HandleMovement()
        {
            if (isDashing) return;
            Vector2 moveInput = InputManager.Instance.MoveInput;
            float currentSpeed = PlayerManager.Instance.playerData.moveSpeed;
            rb.velocity = moveInput * currentSpeed;
        }

        private void HandleDashInput()
        {
            if (InputManager.Instance.Dash && !isDashing && Time.time >= lastDashTime + PlayerManager.Instance.playerData.dashCooldown)
            {
                StartCoroutine(DashCoroutine());
            }
        }

        private void HandleSpriteFlip()
        {
            Vector2 moveInput = InputManager.Instance.MoveInput;
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
            Vector2 moveInput = InputManager.Instance.MoveInput;
            Vector2 dashDirection = moveInput.normalized;
            if (dashDirection == Vector2.zero)
            {
                dashDirection = isFacingRight ? Vector2.right : Vector2.left;
            }
            rb.velocity = dashDirection * PlayerManager.Instance.playerData.dashSpeed;
            yield return new WaitForSeconds(PlayerManager.Instance.playerData.dashDuration);
            isDashing = false;
        }
    }
}