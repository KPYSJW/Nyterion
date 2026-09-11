using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class MushroomEnemyProjectile : MonoBehaviour
    {
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private Transform explosionPoint;
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer shadowRenderer;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.1f;
        [SerializeField, Min(0f)] private float arcHeight = 1.2f;
        [SerializeField, Range(0f, 1f)] private float arcScaleBoost = 0.15f;
        [SerializeField, Range(0f, 1f)] private float playerCollisionStartProgress = 0.75f;
        [SerializeField, Range(0f, 1f)] private float shadowMinimumScale = 0.45f;
        [SerializeField, Range(0f, 1f)] private float shadowMinimumAlpha = 0.12f;

        private Vector2 targetPosition;
        private Vector2 launchPosition;
        private Vector2 launchDirection;
        private float targetDistance;
        private float damage;
        private bool initialized;
        private bool exploded;
        private float flightProgress;
        private Vector3 visualGroundOffset;
        private Vector3 visualBaseScale;
        private Vector3 shadowGroundOffset;
        private Vector3 shadowBaseScale;
        private Color shadowBaseColor;

        public void Initialize(float damageAmount, Vector2 destination)
        {
            damage = damageAmount;
            targetPosition = destination;
            launchPosition = transform.position;
            launchDirection = (targetPosition - launchPosition).normalized;
            targetDistance = Vector2.Distance(launchPosition, targetPosition);
            flightProgress = 0f;

            if (visual != null)
            {
                visualGroundOffset = visual.position - transform.position;
                visualBaseScale = visual.localScale;
            }

            if (shadowRenderer != null)
            {
                Transform shadow = shadowRenderer.transform;
                shadowGroundOffset = shadow.position - transform.position;
                shadowBaseScale = shadow.localScale;
                shadowBaseColor = shadowRenderer.color;
            }

            initialized = true;
            exploded = false;
            UpdateFlightVisuals();
        }

        private void FixedUpdate()
        {
            if (!initialized || exploded) return;

            Vector2 currentPosition = transform.position;
            float traveledDistance = Vector2.Distance(launchPosition, currentPosition);
            flightProgress = targetDistance > 0.001f
                ? Mathf.Clamp01(traveledDistance / targetDistance)
                : 1f;
            UpdateFlightVisuals();

            bool reachedTarget = Vector2.Distance(currentPosition, targetPosition) <= arrivalDistance;
            bool passedTarget = targetDistance > 0f &&
                                traveledDistance >= targetDistance &&
                                Vector2.Dot(targetPosition - currentPosition, launchDirection) <= 0f;

            if (reachedTarget || passedTarget)
            {
                Explode();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (exploded) return;

            if (TryHitPlayer(other))
            {
                return;
            }

            if (other.CompareTag(Tags.Wall))
            {
                Explode(other.ClosestPoint(transform.position));
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (exploded) return;
            TryHitPlayer(other);
        }

        private bool TryHitPlayer(Collider2D other)
        {
            if (flightProgress < playerCollisionStartProgress)
                return false;

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
                return false;

            playerHealth.TakeDamage(damage);
            Explode(other.ClosestPoint(transform.position));
            return true;
        }

        private void UpdateFlightVisuals()
        {
            float arc = 4f * flightProgress * (1f - flightProgress);

            if (visual != null)
            {
                visual.position = transform.position + visualGroundOffset + Vector3.up * (arcHeight * arc);
                visual.localScale = visualBaseScale * (1f + arcScaleBoost * arc);
            }

            if (shadowRenderer != null)
            {
                Transform shadow = shadowRenderer.transform;
                shadow.position = transform.position + shadowGroundOffset;
                shadow.rotation = Quaternion.identity;
                float shadowScale = Mathf.Lerp(1f, shadowMinimumScale, arc);
                shadow.localScale = shadowBaseScale * shadowScale;

                Color shadowColor = shadowBaseColor;
                shadowColor.a = Mathf.Lerp(
                    shadowBaseColor.a,
                    shadowMinimumAlpha,
                    arc);
                shadowRenderer.color = shadowColor;
            }
        }

        private void Explode(Vector2? collisionPoint = null)
        {
            if (exploded) return;
            exploded = true;

            if (explosionPrefab != null)
            {
                Vector3 explosionPosition = collisionPoint.HasValue
                    ? collisionPoint.Value
                    : explosionPoint != null
                        ? explosionPoint.position
                        : transform.position;

                Instantiate(
                    explosionPrefab,
                    explosionPosition,
                    Quaternion.identity);
            }

            Destroy(gameObject);
        }
    }
}
