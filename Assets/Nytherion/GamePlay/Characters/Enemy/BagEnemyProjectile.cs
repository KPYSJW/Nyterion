using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class BagEnemyProjectile : MonoBehaviour
    {
        private const string RandomSpriteResourcesPath =
            "Sprites/Monster/Bag_Ranged/iconset_16x16_00_basic";

        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private Transform explosionPoint;
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private SpriteRenderer shadowRenderer;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.1f;
        [SerializeField, Min(0f)] private float arcHeight = 2.4f;
        [SerializeField, Range(0f, 1f)] private float arcScaleBoost = 0.15f;
        [SerializeField, Range(0f, 1f)] private float playerCollisionStartProgress = 0.75f;
        [SerializeField, Range(0f, 1f)] private float shadowMinimumScale = 0.45f;
        [SerializeField, Range(0f, 1f)] private float shadowMinimumAlpha = 0.12f;
        [SerializeField] private float spinSpeed = 360f;

        private static Sprite[] randomSprites;

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

            AssignRandomSprite();

            if (visual != null)
            {
                visualGroundOffset = visual.position - transform.position;
                visualBaseScale = visual.localScale;
                visual.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
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

            if (visual != null)
            {
                visual.Rotate(0f, 0f, spinSpeed * Time.fixedDeltaTime, Space.Self);
            }

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

            if (TryHitPlayer(other)) return;

            if (other.CompareTag(Tags.Wall))
            {
                Explode(other.ClosestPoint(transform.position));
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!exploded) TryHitPlayer(other);
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

        private void AssignRandomSprite()
        {
            if (visualRenderer == null && visual != null)
                visualRenderer = visual.GetComponent<SpriteRenderer>();

            if (visualRenderer == null) return;

            if (randomSprites == null || randomSprites.Length == 0)
                randomSprites = Resources.LoadAll<Sprite>(RandomSpriteResourcesPath);

            if (randomSprites.Length > 0)
                visualRenderer.sprite = randomSprites[Random.Range(0, randomSprites.Length)];
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
                shadow.localScale = shadowBaseScale * Mathf.Lerp(1f, shadowMinimumScale, arc);

                Color shadowColor = shadowBaseColor;
                shadowColor.a = Mathf.Lerp(shadowBaseColor.a, shadowMinimumAlpha, arc);
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

                Instantiate(explosionPrefab, explosionPosition, Quaternion.identity);
            }

            Destroy(gameObject);
        }
    }
}
