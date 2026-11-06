using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossWitchProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 6f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.22f;
        [SerializeField, Min(0.1f)] private float lifetime = 5f;
        [SerializeField, Min(1f)] private float framesPerSecond = 14f;
        [Tooltip("이동 방향을 기준으로 원본 이미지의 방향을 보정하는 각도입니다.")]
        [SerializeField] private float visualRotationOffset;
        [SerializeField] private Sprite[] flightSprites;
        [SerializeField] private Sprite[] hitSprites;
        [SerializeField] private LayerMask collisionMask = -1;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);

        private readonly RaycastHit2D[] hits = new RaycastHit2D[64];
        private SpriteRenderer visual;
        private EnemyBase owner;
        private Vector2 direction;
        private float damage;
        private float startedAt;
        private float impactAt;
        private bool initialized;
        private bool impacted;
        private System.Action<Vector3, Vector2> impactCallback;

        public Sprite PreviewSprite => flightSprites != null && flightSprites.Length > 0 ? flightSprites[0] : null;

        private void Awake() => visual = GetComponent<SpriteRenderer>();

        public void ConfigureFlight(float travelSpeed, float radius)
        {
            speed = Mathf.Max(0.1f, travelSpeed);
            hitRadius = Mathf.Max(0.01f, radius);
        }

        public void Initialize(EnemyBase source, Vector2 travelDirection, float damageAmount,
            System.Action<Vector3, Vector2> onImpact = null)
        {
            owner = source;
            direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : Vector2.right;
            damage = Mathf.Max(0f, damageAmount);
            startedAt = Time.time;
            initialized = true;
            impacted = false;
            impactCallback = onImpact;
            transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + visualRotationOffset);
            SpriteRenderer sourceRenderer = owner != null ? owner.GetComponent<SpriteRenderer>() : null;
            if (sourceRenderer != null)
            {
                visual.sortingLayerID = sourceRenderer.sortingLayerID;
                visual.sortingOrder = sourceRenderer.sortingOrder + 1;
            }
            visual.sprite = PreviewSprite;
        }

        private void Update()
        {
            if (!initialized) return;
            if (impacted)
            {
                int frame = Mathf.FloorToInt((Time.time - impactAt) * framesPerSecond);
                if (hitSprites == null || frame >= hitSprites.Length)
                {
                    Destroy(gameObject);
                    return;
                }
                visual.sprite = hitSprites[frame];
                return;
            }
            if (Time.time - startedAt >= lifetime || owner == null || owner.isDead || !owner.isActiveAndEnabled)
            {
                Destroy(gameObject);
                return;
            }
            if (flightSprites != null && flightSprites.Length > 0)
                visual.sprite = flightSprites[Mathf.FloorToInt((Time.time - startedAt) * framesPerSecond) % flightSprites.Length];
        }

        private void FixedUpdate()
        {
            if (!initialized || impacted) return;
            if (owner == null || owner.isDead || !owner.isActiveAndEnabled)
            {
                Destroy(gameObject);
                return;
            }
            Vector2 origin = transform.position;
            float distance = speed * Time.fixedDeltaTime;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(collisionMask);
            filter.useTriggers = true;
            int count = Physics2D.CircleCast(origin, hitRadius, direction, filter, hits, distance);
            int nearest = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(owner.transform)) continue;
                if (!IsWall(collider) && !IsTarget(collider)) continue;
                if (hits[i].distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = hits[i].distance;
                }
            }
            if (nearest >= 0)
            {
                RaycastHit2D hit = hits[nearest];
                Vector2 position = origin + direction * hit.distance;
                transform.position = new Vector3(position.x, position.y, transform.position.z);
                // Choose the nearest valid contact first so a wall blocks damage behind it.
                if (!IsWall(hit.collider)) EnemyAttackTarget.TryDamage(hit.collider, damage, hit.point);
                impacted = true;
                impactAt = Time.time;
                System.Action<Vector3, Vector2> callback = impactCallback;
                impactCallback = null;
                callback?.Invoke(transform.position, direction);
                if (hitSprites != null && hitSprites.Length > 0) visual.sprite = hitSprites[0];
                else Destroy(gameObject);
                return;
            }
            Vector2 next = origin + direction * distance;
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        private void OnDrawGizmosSelected()
        {
            if (impacted) return;

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            // CircleCast uses a world-space radius, independent of the sprite's scale.
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 1f);
            Vector3 center = transform.position;
            Vector3 previous = center + Vector3.right * hitRadius;
            const int segments = 64;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * hitRadius;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }

        private bool IsWall(Collider2D collider)
        {
            return collider.CompareTag(Tags.Wall) || (wallLayers.value & (1 << collider.gameObject.layer)) != 0;
        }

        private static bool IsTarget(Collider2D collider)
        {
            DecoyDummy dummy = collider.GetComponentInParent<DecoyDummy>();
            return dummy != null ? dummy.IsAlive : collider.GetComponentInParent<PlayerHealth>() != null;
        }
    }
}
