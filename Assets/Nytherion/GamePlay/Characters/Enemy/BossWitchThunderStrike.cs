using System.Collections.Generic;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossWitchThunderStrike : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer strikeVisual;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 30f;
        [SerializeField, Min(1)] private int damageFrame = 8;
        [SerializeField, Min(0.1f)] private float hitRadius = 1.2f;
        private readonly BossWitchTornadoTimeline timeline = new BossWitchTornadoTimeline();
        private readonly Collider2D[] hits = new Collider2D[64];
        private readonly HashSet<int> damaged = new HashSet<int>();
        private EnemyBase owner;
        private float damage;
        private bool initialized;
        private LineRenderer warning;
        private MeshRenderer warningFill;
        private Mesh warningMesh;

        public void Initialize(EnemyBase source, float delay, float warningDuration, float radius, float damageAmount)
        {
            owner = source;
            hitRadius = Mathf.Max(0.1f, radius);
            damage = Mathf.Max(0f, damageAmount);
            damaged.Clear();
            float duration = frames != null ? frames.Length / framesPerSecond : 0.1f;
            timeline.Start(Time.time + Mathf.Max(0f, delay), warningDuration, duration, 0f, 1f);
            GameObject ring = new GameObject("ThunderStrike Ground Warning");
            ring.transform.SetParent(transform, false);
            warning = ring.AddComponent<LineRenderer>();
            warning.sharedMaterial = warningMaterial;
            warning.useWorldSpace = true;
            warning.loop = true;
            warning.positionCount = 64;
            warning.widthMultiplier = 0.04f;
            warning.startColor = warning.endColor = new Color(1f, 0.85f, 0.1f, 0.9f);
            SpriteRenderer body = owner.GetComponent<SpriteRenderer>();
            strikeVisual.sortingLayerID = warning.sortingLayerID = body != null ? body.sortingLayerID : 0;
            strikeVisual.sortingOrder = body != null ? body.sortingOrder + 2 : 2;
            warning.sortingOrder = strikeVisual.sortingOrder - 3;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64;
                warning.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * hitRadius);
            }
            BuildWarningFill();
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

        private void UpdateVisuals()
        {
            BossWitchTornadoTimeline.Phase phase = timeline.GetPhase(Time.time);
            int frame = Mathf.Max(0, Mathf.FloorToInt((Time.time - timeline.ActiveAt) * framesPerSecond));
            bool active = phase == BossWitchTornadoTimeline.Phase.Active;
            strikeVisual.enabled = active;
            // Show the whole path on impact, including nodes waiting for their propagation delay.
            warning.enabled = phase == BossWitchTornadoTimeline.Phase.Pending ||
                phase == BossWitchTornadoTimeline.Phase.Warning || (active && frame < damageFrame - 1);
            if (warningFill != null) warningFill.enabled = warning.enabled;
            if (active && frames != null && frames.Length > 0) strikeVisual.sprite = frames[Mathf.Min(frame, frames.Length - 1)];
        }

        private void BuildWarningFill()
        {
            const int segments = 64;
            GameObject fillObject = new GameObject("ThunderStrike Warning Fill");
            fillObject.transform.SetParent(transform, false);
            MeshFilter filter = fillObject.AddComponent<MeshFilter>();
            warningFill = fillObject.AddComponent<MeshRenderer>();
            warningFill.sharedMaterial = warningMaterial;
            warningFill.sortingLayerID = warning.sortingLayerID;
            warningFill.sortingOrder = warning.sortingOrder - 1;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", new Color(1f, 0f, 0f, 0.2f));
            warningFill.SetPropertyBlock(properties);
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[segments + 1];
            Color[] colors = new Color[segments + 1];
            int[] triangles = new int[segments * 3];
            colors[0] = Color.white;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[i + 1] = direction * hitRadius;
                uv[i + 1] = direction * 0.5f + uv[0];
                colors[i + 1] = Color.white;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            warningMesh = new Mesh { name = "Witch ThunderStrike Warning Fill" };
            warningMesh.vertices = vertices;
            warningMesh.uv = uv;
            warningMesh.colors = colors;
            warningMesh.triangles = triangles;
            warningMesh.RecalculateBounds();
            filter.sharedMesh = warningMesh;
        }

        private void FixedUpdate()
        {
            if (!initialized) return;
            if (!OwnerAlive) { Cancel(); return; }
            if (timeline.GetPhase(Time.time) != BossWitchTornadoTimeline.Phase.Active ||
                (Time.time - timeline.ActiveAt) * framesPerSecond < damageFrame - 1) return;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            int count = Physics2D.OverlapCircle(transform.position, hitRadius, filter, hits);
            // Keep checking until the animation ends, but hit each target only once per strike.
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = hits[i];
                if (collider == null) continue;
                DecoyDummy dummy = collider.GetComponentInParent<DecoyDummy>();
                PlayerHealth player = dummy == null ? collider.GetComponentInParent<PlayerHealth>() : null;
                if (dummy != null && !dummy.IsAlive) continue;
                if (dummy == null && player == null) continue;
                int id = dummy != null ? dummy.GetInstanceID() : player.GetInstanceID();
                if (damaged.Add(id)) EnemyAttackTarget.TryDamage(collider, damage, collider.ClosestPoint(transform.position));
            }
        }

        public void Cancel()
        {
            initialized = false;
            if (warning != null) warning.enabled = false;
            if (warningFill != null) warningFill.enabled = false;
            if (strikeVisual != null) strikeVisual.enabled = false;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (warningMesh != null) Destroy(warningMesh);
        }

        private void OnDrawGizmosSelected()
        {
            Color color = Gizmos.color;
            Matrix4x4 matrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            Vector3 previous = transform.position + Vector3.right * hitRadius;
            for (int i = 1; i <= 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * hitRadius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
            Gizmos.color = color;
            Gizmos.matrix = matrix;
        }
    }
}
