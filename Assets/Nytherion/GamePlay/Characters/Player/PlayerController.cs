using Nytherion.Core;
using Nytherion.Data.ScriptableObjects.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.GamePlay.Characters.NPC;
using Nytherion.UI.Shop;
using Nytherion.Data.Shop;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("물리 효과를 위한 Rigidbody2D 컴포넌트")]
        [SerializeField] private Rigidbody2D rb;
        [Tooltip("스프라이트 렌더러 컴포넌트")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Interaction")]
        [Tooltip("상호작용을 감지할 거리")]
        [SerializeField] private float interactionDistance = 1.5f;
        [Tooltip("상호작용 가능한 오브젝트들의 레이어")]
        [SerializeField] private LayerMask interactableLayer;

        private bool isFacingRight = true;
        private bool isDashing = false;
        private float lastDashTime = -999f;

        private void OnEnable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onInteract += OnInteract;
            }
        }

        private void OnDisable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onInteract -= OnInteract;
            }
        }

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

        private void OnInteract()
        {
            if (ShopUI.Instance != null && ShopUI.Instance.IsOpen)
            {
                ShopUI.Instance.CloseShop();
                return;
            }
            
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionDistance, interactableLayer);
            foreach (var collider in colliders)
            {
                if (collider.TryGetComponent(out IInteractable interactableObject))
                {
                    interactableObject.Interact();
                    break;
                }
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