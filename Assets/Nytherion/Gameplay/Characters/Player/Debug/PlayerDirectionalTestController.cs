using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerDirectionalTestController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Animator animator;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;

        private Vector2 moveInput;
        private string currentAnimationName;

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void Update()
        {
            moveInput = GetMoveInput();
            UpdateAnimation();
        }

        private void FixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            rb.velocity = moveInput * moveSpeed;
        }

        private Vector2 GetMoveInput()
        {
            if (Keyboard.current == null)
            {
                return Vector2.zero;
            }

            Vector2 input = Vector2.zero;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                input.y += 1f;
            }

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            return input.normalized;
        }

        private void UpdateAnimation()
        {
            if (animator == null || Camera.main == null || Mouse.current == null)
            {
                return;
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

            string animationName = (moveInput.sqrMagnitude > 0.01f ? "Walk_" : "Idle_") +
                                   GetDirectionSuffix(GetDirection(aimDirection));

            if (animationName == currentAnimationName)
            {
                return;
            }

            currentAnimationName = animationName;
            animator.Play(currentAnimationName);
        }

        private string GetDirectionSuffix(Direction direction)
        {
            return direction switch
            {
                Direction.Up => "Up",
                Direction.RightUp => "Right_Up",
                Direction.RightDown => "Right_Down",
                Direction.Down => "Down",
                Direction.LeftDown => "Left_Down",
                Direction.LeftUp => "Left_Up",
                _ => "Down"
            };
        }

        private Direction GetDirection(Vector2 aimDirection)
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            if (angle >= 60f && angle <= 120f)
            {
                return Direction.Up;
            }

            if (angle > 0f && angle < 60f)
            {
                return Direction.RightUp;
            }

            if (angle >= -60f && angle <= 0f)
            {
                return Direction.RightDown;
            }

            if (angle >= -120f && angle < -60f)
            {
                return Direction.Down;
            }

            if (angle >= -180f && angle < -120f)
            {
                return Direction.LeftDown;
            }

            return Direction.LeftUp;
        }

        private enum Direction
        {
            Up,
            RightUp,
            RightDown,
            Down,
            LeftDown,
            LeftUp
        }
    }
}
