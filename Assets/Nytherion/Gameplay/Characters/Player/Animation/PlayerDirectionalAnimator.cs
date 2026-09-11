using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerDirectionalAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private PlayerController playerController;

        private string currentAnimationName;
        private bool wasDashing;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null || animator == null || Camera.main == null || Mouse.current == null)
            {
                return;
            }

            if (playerController != null && playerController.IsDashing)
            {
                wasDashing = true;
                return;
            }

            if (wasDashing)
            {
                currentAnimationName = null;
                wasDashing = false;
            }

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            float cameraDistance = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
            Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(
                new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, cameraDistance));
            Vector2 aimDirection = mouseWorldPosition - transform.position;

            if (aimDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            bool isWalking = rb != null && rb.velocity.sqrMagnitude > 0.01f;
            string animationName = (isWalking ? "Walk_" : "Idle_") + GetDirectionSuffix(aimDirection);

            spriteRenderer.flipX = false;

            if (animationName == currentAnimationName)
            {
                return;
            }

            currentAnimationName = animationName;
            animator.Play(currentAnimationName);
        }

        private string GetDirectionSuffix(Vector2 aimDirection)
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            if (angle >= 45f && angle < 135f)
            {
                return "Up";
            }

            if (angle >= -45f && angle < 45f)
            {
                return "Right";
            }

            if (angle >= -135f && angle < -45f)
            {
                return "Down";
            }

            return "Left";
        }
    }
}
