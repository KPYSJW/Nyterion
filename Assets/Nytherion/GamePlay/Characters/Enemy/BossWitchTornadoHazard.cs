using System.Collections.Generic;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossWitchTornadoHazard : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer tornadoVisual;
        [SerializeField] private Material warningMaterial;
        [SerializeField, Min(0.1f)] private float hitRadius = 0.75f;
        [Tooltip("이미지 크기만 조절합니다. 판정 반경과 예고 원에는 영향을 주지 않습니다.")]
        [SerializeField, Min(0.01f)] private float visualScale = 1f;
        [SerializeField, Min(1f)] private float framesPerSecond = 14f;
        [SerializeField] private Sprite[] spawnSprites;
        [SerializeField] private Sprite[] loopSprites;
        [SerializeField] private Sprite[] dissipateSprites;

        private readonly BossWitchTornadoTimeline timeline = new BossWitchTornadoTimeline();
        private readonly Collider2D[] hits = new Collider2D[64];
        private readonly HashSet<int> damagedTargets = new HashSet<int>();
        private MaterialPropertyBlock fillProperties;
        private EnemyBase owner;
        private float damage;
        private bool initialized;
        private LineRenderer ring;
        private MeshRenderer fill;
        private Mesh warningMesh;
        public float Radius => hitRadius;

        public void Initialize(EnemyBase source, float radius, float warningDuration,
            float duration, float tickInterval, float damagePerTick, float warningDelay = 0f)
        {
            owner = source;
            hitRadius = Mathf.Max(0.1f, radius);
            damage = Mathf.Max(0f, damagePerTick);
            float dissipateDuration = dissipateSprites != null ? dissipateSprites.Length / framesPerSecond : 0f;
            timeline.Start(Time.time + Mathf.Max(0f, warningDelay), warningDuration, duration, dissipateDuration, tickInterval);
            if (tornadoVisual != null)
            {
                tornadoVisual.transform.localScale = Vector3.one * visualScale;
                SpriteRenderer body = owner != null ? owner.GetComponent<SpriteRenderer>() : null;
                if (body != null)
                {
                    tornadoVisual.sortingLayerID = body.sortingLayerID;
                    tornadoVisual.sortingOrder = body.sortingOrder + 1;
                }
            }
            BuildWarning();
            initialized = true;
            UpdateVisuals();
        }

        private bool OwnerAlive => owner != null && owner.isActiveAndEnabled && !owner.isDead;

        private void Update()
        {
            if (!initialized) return;
            if (!OwnerAlive || timeline.GetPhase(Time.time) == BossWitchTornadoTimeline.Phase.Finished)
            {
                Cancel();
                return;
            }
            UpdateVisuals();
        }

        private void FixedUpdate()
        {
            if (!initialized) return;
            if (!OwnerAlive) { Cancel(); return; }
            if (!timeline.TryDamageTick(Time.time)) return;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            int count = Physics2D.OverlapCircle(transform.position, hitRadius, filter, hits);
            damagedTargets.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = hits[i];
                if (collider == null) continue;
                DecoyDummy dummy = collider.GetComponentInParent<DecoyDummy>();
                PlayerHealth player = dummy == null ? collider.GetComponentInParent<PlayerHealth>() : null;
                if (dummy != null && !dummy.IsAlive) continue;
                if (dummy == null && player == null) continue;
                int id = dummy != null ? dummy.GetInstanceID() : player.GetInstanceID();
                if (damagedTargets.Add(id))
                    EnemyAttackTarget.TryDamage(collider, damage, collider.ClosestPoint(transform.position));
            }
        }

        private void UpdateVisuals()
        {
            BossWitchTornadoTimeline.Phase phase = timeline.GetPhase(Time.time);
            bool warning = phase == BossWitchTornadoTimeline.Phase.Warning;
            bool active = phase == BossWitchTornadoTimeline.Phase.Active;
            if (ring != null)
            {
                ring.enabled = warning || active;
                ring.startColor = ring.endColor = warning ? new Color(1f, 0.2f, 0.05f, 0.95f) :
                    new Color(1f, 0.45f, 0.05f, 0.65f);
            }
            if (fill != null)
            {
                fill.enabled = warning || active;
                fillProperties.SetColor("_Color", new Color(1f, 0.2f, 0.02f, warning ? 0.18f : 0.1f));
                fill.SetPropertyBlock(fillProperties);
            }
            if (tornadoVisual == null) return;
            // Keep only the ground warning visible; play the entire growth sequence after it ends.
            if (warning || phase == BossWitchTornadoTimeline.Phase.Pending)
            {
                tornadoVisual.enabled = false;
                return;
            }
            tornadoVisual.enabled = true;
            tornadoVisual.color = Color.white;
            if (active)
            {
                float elapsed = Time.time - timeline.ActiveAt;
                float spawnDuration = spawnSprites != null ? spawnSprites.Length / framesPerSecond : 0f;
                if (elapsed < spawnDuration) ShowFrame(spawnSprites, elapsed, false);
                else ShowFrame(loopSprites, elapsed - spawnDuration, true);
            }
            else ShowFrame(dissipateSprites, Time.time - timeline.ExpiresAt, false);
        }

        private void ShowFrame(Sprite[] sprites, float elapsed, bool loop)
        {
            if (sprites == null || sprites.Length == 0) { tornadoVisual.enabled = false; return; }
            int index = Mathf.Max(0, Mathf.FloorToInt(elapsed * framesPerSecond));
            tornadoVisual.sprite = sprites[loop ? index % sprites.Length : Mathf.Min(index, sprites.Length - 1)];
        }

        private void BuildWarning()
        {
            const int segments = 64;
            GameObject warningObject = new GameObject("Tornado Ground Warning");
            warningObject.transform.SetParent(transform, false);
            MeshFilter meshFilter = warningObject.AddComponent<MeshFilter>();
            fill = warningObject.AddComponent<MeshRenderer>();
            fill.sharedMaterial = warningMaterial;
            fillProperties = new MaterialPropertyBlock();
            GameObject ringObject = new GameObject("Tornado Warning Ring");
            ringObject.transform.SetParent(transform, false);
            ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = warningMaterial;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.widthMultiplier = 0.04f;
            ring.positionCount = segments;
            int sortingLayer = tornadoVisual != null ? tornadoVisual.sortingLayerID : 0;
            int sortingOrder = tornadoVisual != null ? tornadoVisual.sortingOrder - 3 : -2;
            fill.sortingLayerID = ring.sortingLayerID = sortingLayer;
            fill.sortingOrder = sortingOrder;
            ring.sortingOrder = sortingOrder + 1;
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[vertices.Length];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[segments * 3];
            colors[0] = Color.white;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * hitRadius;
                vertices[i + 1] = point;
                colors[i + 1] = Color.white;
                ring.SetPosition(i, transform.position + point);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            warningMesh = new Mesh { name = "Witch Tornado Warning" };
            warningMesh.vertices = vertices;
            warningMesh.uv = uv;
            warningMesh.colors = colors;
            warningMesh.triangles = triangles;
            warningMesh.RecalculateBounds();
            meshFilter.sharedMesh = warningMesh;
        }

        public void Cancel()
        {
            initialized = false;
            if (tornadoVisual != null) tornadoVisual.enabled = false;
            if (ring != null) ring.enabled = false;
            if (fill != null) fill.enabled = false;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (warningMesh != null) Destroy(warningMesh);
        }

        private void OnDrawGizmosSelected()
        {
            Color previous = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.red;
            Vector3 center = transform.position;
            Vector3 last = center + Vector3.right * hitRadius;
            for (int i = 1; i <= 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * hitRadius;
                Gizmos.DrawLine(last, next);
                last = next;
            }
            Gizmos.color = previous;
            Gizmos.matrix = previousMatrix;
        }
    }
}
