using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-10)]
    public sealed class BossGameMasterHomingProjectilePattern : MonoBehaviour, IBossGameMasterPattern
    {
        [Header("Delayed Homing Projectiles / 시간차 추적탄")]
        [SerializeField, Range(1, 64)] private int projectileCount = 8;
        [SerializeField] private Vector2 centerOffset = new Vector2(0f, 1.8f);
        [SerializeField, Min(0.1f)] private float orbitRadius = 2f;
        [SerializeField, Min(0f)] private float orbitSpeed = 30f;
        [SerializeField] private Vector2 fireInterval = new Vector2(0.45f, 0.7f);
        [SerializeField, Min(0.1f)] private float projectileSpeed = 6f;
        [SerializeField, Min(0f)] private float trackingDuration = 3f;
        [SerializeField, Min(0f)] private float turnSpeed = 120f;
        [Tooltip("발사 후 총 수명입니다. 추적 시간보다 짧게 설정해도 추적 시간이 보장됩니다.")]
        [SerializeField, Min(0.1f)] private float flightLifetime = 7f;
        [SerializeField, Min(0.1f)] private float projectileScale = 0.8f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.3f;
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField, Min(1f)] private float framesPerSecond = 16f;
        [Tooltip("메테오 원본은 아래쪽을 바라보므로 90도 보정합니다.")]
        [SerializeField] private float spriteAngleOffset = 90f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);
        [SerializeField] private Sprite[] projectileFrames;
        [SerializeField] private Material projectileMaterial;
        [Header("Impact / 충돌 이펙트")]
        [SerializeField] private Sprite[] impactFrames;
        [SerializeField, Min(1f)] private float impactFramesPerSecond = 24f;
        [SerializeField, Min(0.1f)] private float impactScale = 1f;
        private BossGameMasterEnemy owner;
        private EnemyAIController ai;
        private GameObject root;
        private readonly List<BossGameMasterHomingProjectile> projectiles = new List<BossGameMasterHomingProjectile>();
        private readonly List<int> releaseOrder = new List<int>();
        private int releasedCount;
        private float beganAt, nextFireAt, initialAngle, visualExtent;
        public BossGameMasterPatternType PatternType => BossGameMasterPatternType.DelayedHomingProjectiles;
        public bool HasSpawned => root != null;

        public bool CanStart()
        {
            if (owner == null) owner = GetComponent<BossGameMasterEnemy>();
            if (ai == null) ai = GetComponent<EnemyAIController>();
            if (!isActiveAndEnabled || owner == null || owner.isDead || owner.IsUpgrading || ai == null ||
                ai.CurrentTarget == null || projectileMaterial == null || projectileFrames == null || projectileFrames.Length == 0) return false;
            foreach (Sprite frame in projectileFrames) if (frame == null) return false;
            return true;
        }

        private float NextInterval()
        {
            float low = Mathf.Max(0.01f, Mathf.Min(fireInterval.x, fireInterval.y));
            return Random.Range(low, Mathf.Max(low, Mathf.Max(fireInterval.x, fireInterval.y)));
        }

        public void Begin()
        {
            Clear();
            if (!CanStart()) return;
            root = new GameObject("GameMaster Delayed Homing Projectiles");
            beganAt = Time.time;
            nextFireAt = Time.time + NextInterval();
            initialAngle = Random.Range(0f, 360f);
            visualExtent = 0f;
            foreach (Sprite frame in projectileFrames)
                visualExtent = Mathf.Max(visualExtent, (frame.bounds.center.magnitude + frame.bounds.extents.magnitude) * Mathf.Max(0.1f, projectileScale));
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            for (int i = 0; i < Mathf.Clamp(projectileCount, 1, 64); i++)
            {
                GameObject node = new GameObject("Homing Projectile " + (i + 1));
                node.layer = 2;
                node.transform.SetParent(root.transform, false);
                BossGameMasterHomingProjectile shot = node.AddComponent<BossGameMasterHomingProjectile>();
                shot.Initialize(owner, ai, projectileSpeed, turnSpeed, trackingDuration, flightLifetime, projectileScale,
                    hitRadius, damage, framesPerSecond, spriteAngleOffset, wallLayers, projectileFrames, projectileMaterial,
                    body != null ? body.sortingLayerID : 0, body != null ? body.sortingOrder + 8 : 28);
                shot.ConfigureImpact(impactFrames, impactFramesPerSecond, impactScale);
                projectiles.Add(shot); releaseOrder.Add(i);
            }
            for (int i = 0; i < releaseOrder.Count; i++)
            {
                int chosen = Random.Range(i, releaseOrder.Count);
                int value = releaseOrder[i]; releaseOrder[i] = releaseOrder[chosen]; releaseOrder[chosen] = value;
            }
            UpdateWaitingPositions();
        }

        private void UpdateWaitingPositions()
        {
            Vector2 center = transform.TransformPoint(centerOffset);
            for (int i = 0; i < projectiles.Count; i++)
            {
                BossGameMasterHomingProjectile shot = projectiles[i];
                if (shot == null || shot.IsFinished || shot.IsReleased) continue;
                float angle = (initialAngle + i * 360f / projectiles.Count + (Time.time - beganAt) * orbitSpeed) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = Mathf.Max(0.1f, orbitRadius);
                RaycastHit2D wall = Physics2D.CircleCast(center, visualExtent, direction, distance, wallLayers);
                if (wall.collider != null) distance = Mathf.Max(0f, wall.distance - 0.05f);
                Vector2 tangent = new Vector2(-direction.y, direction.x) * (orbitSpeed < 0f ? -1f : 1f);
                shot.SetWaitingPosition(center + direction * distance, tangent);
            }
        }

        public bool Tick()
        {
            if (root == null || owner == null || !owner.isActiveAndEnabled || owner.isDead || owner.IsUpgrading) { Clear(); return true; }
            UpdateWaitingPositions();
            if (releasedCount < releaseOrder.Count && Time.time >= nextFireAt)
            {
                BossGameMasterHomingProjectile shot = projectiles[releaseOrder[releasedCount++]];
                if (shot != null && !shot.IsFinished) shot.Fire();
                // Never dump missed volleys into one frame after a pause.
                nextFireAt = Time.time + NextInterval();
            }
            foreach (BossGameMasterHomingProjectile shot in projectiles)
                if (shot != null && !shot.IsFinished) return false;
            Clear();
            return true;
        }

        private void FixedUpdate()
        {
            if (root != null) UpdateWaitingPositions();
        }

        public void Clear()
        {
            foreach (BossGameMasterHomingProjectile shot in projectiles) if (shot != null) shot.Cancel();
            if (root != null) { root.SetActive(false); Destroy(root); }
            root = null;
            projectiles.Clear(); releaseOrder.Clear(); releasedCount = 0;
        }

        private void OnDisable() => Clear();
    }
}
