using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class BoomerangEffect : MonoBehaviour
    {
        [Header("Boomerang Settings")]
        public float flyTime = 0.5f;

        public float maxReturnSpeed = 15f;
        public float returnAcceleration = 30f;

        private float currentFlyTime = 0f;
        private bool isReturning = false;
        private bool isInitialized = false;

        private Vector3 startPosition;
        private Vector2 returnDirection;
        private Vector2 initialVelocity;
        private float currentReturnSpeed = 0f;

        private Rigidbody2D rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            currentFlyTime = 0f;
            currentReturnSpeed = 0f;
            isReturning = false;
            isInitialized = false;
        }

        private void Update()
        {
            if (rb == null) return;

            if (!isInitialized)
            {
                if (rb.velocity.sqrMagnitude > 0.01f)
                {
                    startPosition = transform.position;
                    initialVelocity = rb.velocity;
                    isInitialized = true;
                }
                return;
            }

            if (!isReturning)
            {
                currentFlyTime += Time.deltaTime;

                float t = Mathf.Clamp01(currentFlyTime / flyTime);

                rb.velocity = Vector2.Lerp(initialVelocity, Vector2.zero, t);

                if (currentFlyTime >= flyTime)
                {
                    isReturning = true;
                    returnDirection = (startPosition - transform.position).normalized;
                }
            }
            else
            {
                currentReturnSpeed += returnAcceleration * Time.deltaTime;
                currentReturnSpeed = Mathf.Min(currentReturnSpeed, maxReturnSpeed);

                rb.velocity = returnDirection * currentReturnSpeed;

                float angle = Mathf.Atan2(returnDirection.y, returnDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            }
        }
    }
}