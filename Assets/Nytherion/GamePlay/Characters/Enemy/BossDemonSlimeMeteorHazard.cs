using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossDemonSlimeMeteorHazard : MonoBehaviour
    {
        [SerializeField] private Sprite[] meteorFrames;
        [SerializeField] private Sprite[] impactFrames;
        [SerializeField] private Material effectMaterial;
        // Maximum visible radius across Fire_Effect_07 frames (alpha > 8), excluding transparent padding.
        [SerializeField, HideInInspector] private float impactPixelRadius = 97.5833f;
        private EnemyBase owner;
        private bool initialized, landed;
        private float startsAt, fallsAt, landsAt, impactStartedAt;
        private float height, dropDuration, bodyScale, groundScale, fallFps, impactFps, damage, hitRadius;
        private LayerMask wallLayers;
        private SpriteRenderer fallingVisual, impactVisual;
        private GameObject warningObject;
        private Mesh warningMesh;
        private readonly List<Collider2D> hits = new List<Collider2D>(32);
        private readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>(16);
        private readonly HashSet<int> damaged = new HashSet<int>();

        public bool HasSprites => meteorFrames != null && meteorFrames.Length > 0 && meteorFrames[0] != null &&
            impactFrames != null && impactFrames.Length > 0 && impactFrames[0] != null && effectMaterial != null;
        public float GetImpactRadius(float scale) => HasSprites ? Mathf.Max(0.1f, scale) *
            impactPixelRadius / Mathf.Max(0.01f, impactFrames[0].pixelsPerUnit) : 0f;

        public void Initialize(EnemyBase source, float impactScale, float meteorScale, float warningDuration,
            float fallDuration, float fallHeight, float fallFramesPerSecond, float impactFramesPerSecond,
            float amount, float delay, LayerMask walls)
        {
            if (!HasSprites || source == null) { Cancel(); return; }
            owner = source;
            startsAt = Time.time + Mathf.Max(0f, delay);
            fallsAt = startsAt + Mathf.Max(0f, warningDuration);
            dropDuration = Mathf.Max(0.05f, fallDuration);
            landsAt = fallsAt + dropDuration;
            height = Mathf.Max(0.1f, fallHeight);
            bodyScale = Mathf.Max(0.1f, meteorScale);
            groundScale = Mathf.Max(0.1f, impactScale);
            fallFps = Mathf.Max(1f, fallFramesPerSecond);
            impactFps = Mathf.Max(1f, impactFramesPerSecond);
            damage = Mathf.Max(0f, amount);
            wallLayers = walls;
            hitRadius = GetImpactRadius(groundScale);
            landed = false;
            damaged.Clear();
            SpriteRenderer body = source.GetComponent<SpriteRenderer>();
            fallingVisual = CreateVisual("Falling Meteor / Fire_Effect_04", body, 2);
            impactVisual = CreateVisual("Ground Impact / Fire_Effect_07", body, -1);
            fallingVisual.transform.localScale = Vector3.one * bodyScale;
            impactVisual.transform.localScale = Vector3.one * groundScale;
            BuildWarning(body);
            initialized = true;
            Update();
        }

        private SpriteRenderer CreateVisual(string label, SpriteRenderer body, int orderOffset)
        {
            GameObject child = new GameObject(label);
            child.layer = 2;
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = effectMaterial;
            renderer.sortingLayerID = body != null ? body.sortingLayerID : 0;
            renderer.sortingOrder = (body != null ? body.sortingOrder : 0) + orderOffset;
            renderer.enabled = false;
            return renderer;
        }

        private void Update()
        {
            if (!initialized) return;
            if (owner == null || !owner.isActiveAndEnabled || owner.isDead) { Cancel(); return; }
            if (Time.time < startsAt) return;
            if (!landed)
            {
                warningObject.SetActive(true);
                if (Time.time < fallsAt) return;
                if (Time.time < landsAt)
                {
                    int frame = Mathf.Max(0, Mathf.FloorToInt((Time.time - fallsAt) * fallFps)) % meteorFrames.Length;
                    fallingVisual.enabled = true;
                    fallingVisual.sprite = meteorFrames[frame];
                    float progress = Mathf.Clamp01((Time.time - fallsAt) / dropDuration);
                    fallingVisual.transform.localPosition = Vector3.up * (height * (1f - progress * progress));
                    return;
                }
                landed = true;
                impactStartedAt = Time.time;
                fallingVisual.enabled = false;
                warningObject.SetActive(false);
            }
            int impactFrame = Mathf.Max(0, Mathf.FloorToInt((Time.time - impactStartedAt) * impactFps));
            if (impactFrame >= impactFrames.Length) { Cancel(); return; }
            Sprite sprite = impactFrames[impactFrame];
            impactVisual.enabled = true;
            impactVisual.sprite = sprite;
            // Center the ground effect even if the supplied sprites use a custom pivot.
            impactVisual.transform.localPosition = -sprite.bounds.center * groundScale;
            DamageImpact();
        }

        private void DamageImpact()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            Physics2D.OverlapCircle(transform.position, hitRadius, filter, hits);
            foreach (Collider2D collider in hits)
            {
                if (collider == null || collider.transform.IsChildOf(owner.transform)) continue;
                DecoyDummy dummy = collider.GetComponentInParent<DecoyDummy>();
                PlayerHealth player = dummy == null ? collider.GetComponentInParent<PlayerHealth>() : null;
                if (dummy != null ? !dummy.IsAlive : player == null || player.CurrentHealth <= 0f) continue;
                int id = dummy != null ? dummy.GetInstanceID() : player.GetInstanceID();
                Vector2 point = collider.ClosestPoint(transform.position);
                if (damaged.Contains(id) || Vector2.Distance(point, transform.position) > hitRadius || BlockedByWall(point)) continue;
                damaged.Add(id);
                EnemyAttackTarget.TryDamage(collider, damage, point);
            }
        }

        private bool BlockedByWall(Vector2 point)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = false;
            Physics2D.Linecast(transform.position, point, filter, wallHits);
            foreach (RaycastHit2D hit in wallHits)
                if (hit.collider != null && (hit.collider.CompareTag(Tags.Wall) ||
                    (wallLayers.value & (1 << hit.collider.gameObject.layer)) != 0)) return true;
            return false;
        }

        private void BuildWarning(SpriteRenderer body)
        {
            const int segments = 64;
            warningObject = new GameObject("Meteor Landing Warning");
            warningObject.layer = 2;
            warningObject.transform.SetParent(transform, false);
            MeshRenderer fill = warningObject.AddComponent<MeshRenderer>();
            fill.sharedMaterial = effectMaterial;
            // Unity objects must be created here on the main thread, never in field initializers.
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", new Color(1f, 0f, 0f, 0.2f));
            fill.SetPropertyBlock(properties);
            GameObject outline = new GameObject("Warning Edge");
            outline.layer = 2;
            outline.transform.SetParent(warningObject.transform, false);
            LineRenderer edge = outline.AddComponent<LineRenderer>();
            edge.sharedMaterial = effectMaterial;
            edge.useWorldSpace = false;
            edge.loop = true;
            edge.widthMultiplier = 0.045f;
            edge.positionCount = segments;
            edge.startColor = edge.endColor = new Color(1f, 0.15f, 0.02f, 0.85f);
            fill.sortingLayerID = edge.sortingLayerID = body != null ? body.sortingLayerID : 0;
            fill.sortingOrder = (body != null ? body.sortingOrder : 0) - 3;
            edge.sortingOrder = fill.sortingOrder + 1;
            Vector3[] vertices = new Vector3[segments + 1];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 3];
            colors[0] = Color.white;
            for (int i = 0; i < segments; i++)
            {
                Vector2 point = BossDemonSlimeAttackArea.CirclePoint(Vector2.zero, hitRadius, i / (float)segments);
                vertices[i + 1] = point;
                colors[i + 1] = Color.white;
                uv[i + 1] = new Vector2(0.5f, 0.5f);
                edge.SetPosition(i, point);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            warningMesh = new Mesh { name = "Meteor Warning Disk" };
            warningMesh.vertices = vertices;
            warningMesh.colors = colors;
            warningMesh.uv = uv;
            warningMesh.triangles = triangles;
            warningMesh.RecalculateBounds();
            warningObject.AddComponent<MeshFilter>().sharedMesh = warningMesh;
            warningObject.SetActive(false);
        }

        public void Cancel()
        {
            initialized = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        private void OnDisable() { initialized = false; }
        private void OnDestroy() { if (warningMesh != null) Destroy(warningMesh); }
        private void OnDrawGizmosSelected()
        {
            if (!initialized) return;
            Color old = Gizmos.color;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, hitRadius);
            Gizmos.color = old;
        }
    }
}
