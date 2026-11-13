using System.Collections.Generic;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossWitchIcePillarHazard : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer pillarVisual;
        [SerializeField] private Material warningMaterial;
        [SerializeField, Min(1f)] private float framesPerSecond = 14f;
        [SerializeField, Min(0.1f)] private float hitRadius = 0.85f;
        [SerializeField, Min(0.1f)] private float blockingRadius = 0.65f;
        [SerializeField, Min(0.01f)] private float visualScale = 0.75f;
        [Header("Rise Knockback")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 10f;
        [SerializeField, Min(0.01f)] private float knockbackDuration = 0.25f;
        [SerializeField] private Sprite[] riseSprites;
        [SerializeField] private Sprite standingSprite;
        [SerializeField] private Sprite[] breakSprites;

        private readonly BossWitchTornadoTimeline timeline = new BossWitchTornadoTimeline();
        private readonly Collider2D[] hits = new Collider2D[64];
        private readonly HashSet<int> damaged = new HashSet<int>();
        private EnemyBase owner;
        private float damage;
        private bool initialized;
        private bool rose;
        private CircleCollider2D solid;
        private LineRenderer warning;

        public void Initialize(EnemyBase source, float warningSeconds, float duration,
            float damageRadius, float obstacleRadius, float damageAmount, float warningDelay = 0f,
            int sortingOrderOffset = 1)
        {
            owner = source;
            hitRadius = Mathf.Max(0.1f, damageRadius);
            blockingRadius = Mathf.Max(0.1f, obstacleRadius);
            damage = Mathf.Max(0f, damageAmount);
            timeline.Start(Time.time + Mathf.Max(0f, warningDelay), warningSeconds, duration,
                breakSprites != null ? breakSprites.Length / framesPerSecond : 0f, 1f);
            solid = gameObject.AddComponent<CircleCollider2D>();
            solid.radius = blockingRadius;
            solid.isTrigger = false;
            solid.enabled = false;
            // PlayerOnlyObstacle collides with the player body, without carving enemy navigation.
            pillarVisual.transform.localScale = Vector3.one * visualScale;
            SpriteRenderer body = owner.GetComponent<SpriteRenderer>();
            if (body != null)
            {
                pillarVisual.sortingLayerID = body.sortingLayerID;
                pillarVisual.sortingOrder = body.sortingOrder + sortingOrderOffset;
            }
            GameObject warningObject = new GameObject("Ice Pillar Warning");
            warningObject.transform.SetParent(transform, false);
            warning = warningObject.AddComponent<LineRenderer>();
            warning.sharedMaterial = warningMaterial;
            warning.useWorldSpace = true;
            warning.loop = true;
            warning.positionCount = 64;
            warning.widthMultiplier = 0.05f;
            warning.startColor = warning.endColor = new Color(0.2f, 0.8f, 1f, 0.95f);
            warning.sortingLayerID = pillarVisual.sortingLayerID;
            warning.sortingOrder = pillarVisual.sortingOrder - 2;
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2f / 64;
                warning.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * hitRadius);
            }
            initialized = true;
            UpdateVisuals();
        }

        private bool OwnerAlive => owner != null && owner.isActiveAndEnabled && !owner.isDead;

        private void Update()
        {
            if (!initialized) return;
            if (!OwnerAlive || timeline.GetPhase(Time.time) == BossWitchTornadoTimeline.Phase.Finished)
            { Cancel(); return; }
            UpdateVisuals();
        }

        private void FixedUpdate()
        {
            if (!initialized) return;
            if (!OwnerAlive) { Cancel(); return; }
            if (timeline.GetPhase(Time.time) != BossWitchTornadoTimeline.Phase.Active)
            { DisableBlocker(); return; }
            if (!rose)
            {
                rose = true;
                ApplyRiseDamage();
            }
            EnableBlocker();
        }

        private int Overlaps(float radius)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            return Physics2D.OverlapCircle(transform.position, radius, filter, hits);
        }

        private void ApplyRiseDamage()
        {
            int count = Overlaps(hitRadius);
            damaged.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = hits[i];
                if (collider == null) continue;
                DecoyDummy dummy = collider.GetComponentInParent<DecoyDummy>();
                PlayerHealth player = dummy == null ? collider.GetComponentInParent<PlayerHealth>() : null;
                if (dummy != null && !dummy.IsAlive) continue;
                if (dummy == null && player == null) continue;
                int id = dummy != null ? dummy.GetInstanceID() : player.GetInstanceID();
                if (!damaged.Add(id)) continue;
                EnemyAttackTarget.TryDamage(collider, damage, collider.ClosestPoint(transform.position));
                if (player != null)
                {
                    PlayerController controller = collider.GetComponentInParent<PlayerController>();
                    if (controller != null)
                    {
                        Vector2 direction = player.transform.position - transform.position;
                        if (direction.sqrMagnitude < 0.0001f && owner != null)
                            direction = player.transform.position - owner.transform.position;
                        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
                        controller.ApplyKnockback(direction.normalized, knockbackSpeed, knockbackDuration);
                    }
                }
            }
        }

        private void EnableBlocker()
        {
            if (solid.enabled) return;
            // Rise damage and knockback are applied first; the pillar becomes solid immediately.
            solid.enabled = true;
        }

        private void DisableBlocker()
        {
            if (solid != null) solid.enabled = false;
        }

        private void UpdateVisuals()
        {
            BossWitchTornadoTimeline.Phase phase = timeline.GetPhase(Time.time);
            bool warningPhase = phase == BossWitchTornadoTimeline.Phase.Warning;
            warning.enabled = warningPhase;
            bool beforeRise = warningPhase || phase == BossWitchTornadoTimeline.Phase.Pending;
            pillarVisual.enabled = !beforeRise;
            if (beforeRise) return;
            if (phase == BossWitchTornadoTimeline.Phase.Active)
            {
                int frame = Mathf.Max(0, Mathf.FloorToInt((Time.time - timeline.ActiveAt) * framesPerSecond));
                pillarVisual.sprite = riseSprites != null && frame < riseSprites.Length ? riseSprites[frame] : standingSprite;
            }
            else
            {
                DisableBlocker();
                int frame = Mathf.Max(0, Mathf.FloorToInt((Time.time - timeline.ExpiresAt) * framesPerSecond));
                if (breakSprites != null && breakSprites.Length > 0)
                    pillarVisual.sprite = breakSprites[Mathf.Min(frame, breakSprites.Length - 1)];
            }
        }

        public void Cancel()
        {
            initialized = false;
            DisableBlocker();
            if (warning != null) warning.enabled = false;
            if (pillarVisual != null) pillarVisual.enabled = false;
            Destroy(gameObject);
        }

        private void OnDisable() => DisableBlocker();

        private void OnDrawGizmosSelected()
        {
            Color color = Gizmos.color;
            Matrix4x4 matrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            foreach (float radius in new[] { hitRadius, blockingRadius })
            {
                Gizmos.color = radius == hitRadius ? Color.red : Color.cyan;
                Vector3 previous = transform.position + Vector3.right * radius;
                for (int i = 1; i <= 64; i++)
                {
                    float angle = i * Mathf.PI * 2f / 64;
                    Vector3 next = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                    Gizmos.DrawLine(previous, next);
                    previous = next;
                }
            }
            Gizmos.color = color;
            Gizmos.matrix = matrix;
        }
    }
}
