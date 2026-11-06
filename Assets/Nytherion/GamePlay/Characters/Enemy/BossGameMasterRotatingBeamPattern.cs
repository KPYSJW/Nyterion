using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-10)]
    public sealed class BossGameMasterRotatingBeamPattern : MonoBehaviour, IBossGameMasterPattern
    {
        public enum RotationDirection { Clockwise, Counterclockwise, Random }
        [Header("Rotating Beams / 회전 광선")]
        [SerializeField, Range(1, 16)] private int orbCount = 3;
        [SerializeField] private Vector2 centerOffset = new Vector2(0f, 1.8f);
        [SerializeField, Min(0.1f)] private float orbitRadius = 2f;
        [SerializeField, Min(0.1f)] private float orbSize = 1.5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 30f;
        [SerializeField] private RotationDirection rotationDirection = RotationDirection.Random;
        [SerializeField, Min(0.05f)] private float warningDuration = 1f;
        [SerializeField, Min(0.05f)] private float holdDuration = 6f;
        [SerializeField, Min(0.01f)] private float launchDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float beamWidth = 0.7f;
        [SerializeField, Min(1f)] private float maximumBeamLength = 100f;
        [SerializeField, Min(0f)] private float wallPadding = 0.05f;
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField, Min(0.05f)] private float damageInterval = 0.5f;
        [SerializeField, Min(0.1f)] private float beamAnimationSpeed = 1f;
        [SerializeField, Min(1f)] private float framesPerSecond = 24f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);
        [Header("Visuals / 이미지")]
        [SerializeField] private Sprite[] orbFrames, startFrames, bodyFrames, endFrames;
        [SerializeField] private Material orbMaterial, beamMaterial, warningMaterial;

        private BossGameMasterEnemy owner;
        private GameObject root;
        private readonly List<SpriteRenderer> orbs = new List<SpriteRenderer>();
        private readonly List<BossGameMasterLinkBeam> beams = new List<BossGameMasterLinkBeam>();
        private readonly List<LineRenderer> warnings = new List<LineRenderer>();
        private MaterialPropertyBlock properties;
        private Vector2 center;
        private float radius, orbExtent, initialAngle, signedSpeed, beganAt, firedAt;
        private bool firing, finishing;
        public BossGameMasterPatternType PatternType => BossGameMasterPatternType.RotatingBeams;
        public bool HasFired => root != null && firing;

        public bool CanStart()
        {
            if (owner == null) owner = GetComponent<BossGameMasterEnemy>();
            return isActiveAndEnabled && owner != null && owner.isActiveAndEnabled && !owner.isDead && !owner.IsUpgrading &&
                orbFrames != null && orbFrames.Length > 0 && startFrames != null && startFrames.Length >= 21 &&
                bodyFrames != null && bodyFrames.Length >= 13 && endFrames != null && endFrames.Length >= 13 &&
                orbMaterial != null && beamMaterial != null && warningMaterial != null;
        }

        public void Begin()
        {
            Clear();
            if (!CanStart()) return;
            orbExtent = 0f;
            foreach (Sprite frame in orbFrames)
                orbExtent = Mathf.Max(orbExtent, frame.bounds.extents.magnitude * Mathf.Max(0.1f, orbSize));
            if (!UpdateOrbitCenter()) return;

            initialAngle = Random.Range(0f, 360f);
            bool clockwise = rotationDirection == RotationDirection.Clockwise ||
                (rotationDirection == RotationDirection.Random && Random.Range(0, 2) == 0);
            signedSpeed = Mathf.Max(0f, rotationSpeed) * (clockwise ? -1f : 1f);
            beganAt = Time.time;
            root = new GameObject("GameMaster Rotating Beams");
            properties = new MaterialPropertyBlock();
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            int layer = body != null ? body.sortingLayerID : 0;
            int order = body != null ? body.sortingOrder + 4 : 24;
            int count = Mathf.Clamp(orbCount, 1, 16);
            for (int i = 0; i < count; i++)
            {
                GameObject node = new GameObject("Rotating Orb " + (i + 1));
                node.transform.SetParent(root.transform, false);
                node.transform.localScale = Vector3.one * Mathf.Max(0.1f, orbSize);
                SpriteRenderer orb = node.AddComponent<SpriteRenderer>();
                orb.sharedMaterial = orbMaterial;
                orb.sprite = orbFrames[0];
                orb.sortingLayerID = layer;
                orb.sortingOrder = order;
                orbs.Add(orb);
                GetSegment(i, count, 0f, out Vector2 a, out Vector2 b);
                GameObject effect = new GameObject("Rotating Beam " + (i + 1));
                effect.transform.SetParent(root.transform, false);
                BossGameMasterLinkBeam beam = effect.AddComponent<BossGameMasterLinkBeam>();
                beam.Initialize(owner, a, b, beamWidth, launchDuration, warningDuration, damage, damageInterval,
                    beamAnimationSpeed, startFrames, bodyFrames, endFrames, beamMaterial, orbMaterial, layer, order + 1);
                beams.Add(beam);
                GameObject cue = new GameObject("Rotation Warning " + (i + 1));
                cue.transform.SetParent(root.transform, false);
                LineRenderer warning = cue.AddComponent<LineRenderer>();
                warning.sharedMaterial = warningMaterial;
                warning.positionCount = 2;
                warning.useWorldSpace = true;
                warning.widthMultiplier = Mathf.Max(0.05f, beamWidth);
                warning.startColor = warning.endColor = new Color(1f, 0.05f, 0.02f, 0.25f);
                warning.sortingLayerID = layer;
                warning.sortingOrder = order - 2;
                warnings.Add(warning);
            }
            UpdateGeometry();
        }

        private bool UpdateOrbitCenter()
        {
            center = transform.TransformPoint(centerOffset);
            // Recheck the full orbit at the boss's current position; restore the configured radius in open space.
            radius = Mathf.Max(0.1f, orbitRadius);
            float clearance = orbExtent + Mathf.Max(0f, wallPadding);
            for (int i = 0; i < 12 && Physics2D.OverlapCircle(center, radius + clearance, wallLayers) != null; i++)
                radius *= 0.75f;
            return Physics2D.OverlapCircle(center, radius + clearance, wallLayers) == null;
        }

        private void GetSegment(int index, int count, float elapsed, out Vector2 from, out Vector2 to)
        {
            float angle = (initialAngle + 360f * index / count + signedSpeed * elapsed) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            from = center + direction * radius;
            float length = Mathf.Max(1f, maximumBeamLength);
            // Cast the beam's full damage width so the hit box cannot reach through a wall edge.
            RaycastHit2D wall = Physics2D.CircleCast(from, Mathf.Max(0.05f, beamWidth) * 0.5f,
                direction, length, wallLayers);
            if (wall.collider != null) length = Mathf.Max(0f, wall.distance - Mathf.Max(0f, wallPadding));
            to = from + direction * length;
        }

        private void UpdateGeometry()
        {
            if (!UpdateOrbitCenter()) { Clear(); return; }
            float elapsed = firing ? Mathf.Clamp(Time.time - firedAt, 0f, Mathf.Max(0.05f, holdDuration)) : 0f;
            for (int i = 0; i < beams.Count; i++)
            {
                GetSegment(i, beams.Count, elapsed, out Vector2 a, out Vector2 b);
                orbs[i].transform.position = a;
                if (beams[i] != null) beams[i].SetEndpoints(a, b);
                warnings[i].SetPosition(0, a);
                warnings[i].SetPosition(1, b);
            }
        }

        public bool Tick()
        {
            if (root == null || owner == null || !owner.isActiveAndEnabled || owner.isDead || owner.IsUpgrading)
            { Clear(); return true; }
            int frame = Mathf.FloorToInt((Time.time - beganAt) * Mathf.Max(1f, framesPerSecond)) % orbFrames.Length;
            foreach (SpriteRenderer orb in orbs)
            {
                orb.sprite = orbFrames[frame];
                orb.GetPropertyBlock(properties);
                properties.SetTexture("_MainTex", orb.sprite.texture);
                properties.SetFloat("_FlashAmount", 0f);
                orb.SetPropertyBlock(properties);
            }
            UpdateGeometry();
            if (root == null) return true;
            if (finishing)
            {
                foreach (BossGameMasterLinkBeam beam in beams)
                    if (beam != null && !beam.IsFinished) return false;
                Clear();
                return true;
            }
            if (!firing)
            {
                foreach (BossGameMasterLinkBeam beam in beams)
                    if (beam == null || !beam.CanFire) return false;
                foreach (BossGameMasterLinkBeam beam in beams) beam.Fire();
                foreach (LineRenderer warning in warnings) warning.enabled = false;
                firedAt = Time.time;
                firing = true;
            }
            if (Time.time - firedAt >= Mathf.Max(0.05f, holdDuration))
            {
                finishing = true;
                foreach (BossGameMasterLinkBeam beam in beams) if (beam != null) beam.BeginEnd();
            }
            return false;
        }

        // Runs before beam damage queries, keeping physics and visible endpoints in agreement.
        private void FixedUpdate()
        {
            if (root != null && owner != null && !owner.isDead && !owner.IsUpgrading) UpdateGeometry();
        }

        public void Clear()
        {
            foreach (BossGameMasterLinkBeam beam in beams) if (beam != null) beam.Cancel();
            if (root != null) { root.SetActive(false); Destroy(root); }
            root = null;
            beams.Clear(); orbs.Clear(); warnings.Clear();
            firing = finishing = false;
        }

        private void OnDisable() => Clear();
    }
}
