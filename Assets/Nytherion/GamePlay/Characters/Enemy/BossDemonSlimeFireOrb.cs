using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossDemonSlimeFireOrb : MonoBehaviour
    {
        [SerializeField] private BossWitchProjectile projectilePrefab;
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 16f;
        [SerializeField, Min(0.1f)] private float visualScale = 4f;
        private EnemyBase owner;
        private SpriteRenderer visual;
        private bool initialized, emissionFinished;
        private float startedAt, expiresAt, nextVolleyAt, interval, rotation, startAngle;
        private float speed, scale, radius, damage;
        private int directions, waves, fired;
        private readonly List<BossWitchProjectile> projectiles = new List<BossWitchProjectile>();

        public bool IsConfigured => projectilePrefab != null && frames != null && frames.Length > 0 && frames[0] != null;

        public void Initialize(EnemyBase source, int directionCount, int volleyCount, float volleyInterval,
            float rotationPerVolley, float lifetime, float projectileSpeed, float projectileScale,
            float projectileHitRadius, float damageAmount, float initialAngle)
        {
            if (source == null || !IsConfigured) { Cancel(); return; }
            owner = source;
            visual = GetComponent<SpriteRenderer>();
            SpriteRenderer body = source.GetComponent<SpriteRenderer>();
            visual.sortingLayerID = body != null ? body.sortingLayerID : 0;
            visual.sortingOrder = (body != null ? body.sortingOrder : 0) + 1;
            directions = Mathf.Max(1, directionCount);
            waves = Mathf.Max(1, volleyCount);
            interval = Mathf.Max(0.01f, volleyInterval);
            rotation = rotationPerVolley;
            startAngle = initialAngle;
            speed = Mathf.Max(0.1f, projectileSpeed);
            scale = Mathf.Max(0.1f, projectileScale);
            radius = Mathf.Max(0.01f, projectileHitRadius) * scale;
            damage = Mathf.Max(0f, damageAmount);
            startedAt = Time.time;
            expiresAt = startedAt + Mathf.Max(0.1f, lifetime);
            nextVolleyAt = startedAt + 0.4f;
            fired = 0;
            emissionFinished = false;
            initialized = true;
            visual.sprite = frames[0];
            UpdateVisual();
        }

        private void Update()
        {
            if (!initialized) return;
            if (owner == null || owner.isDead || !owner.isActiveAndEnabled) { Cancel(); return; }
            projectiles.RemoveAll(projectile => projectile == null);
            if (!emissionFinished && Time.time >= expiresAt)
            {
                emissionFinished = true;
                visual.enabled = false;
            }
            if (emissionFinished)
            {
                // Keep ownership until the last released shot expires, so pool reuse can still clear it.
                if (projectiles.Count == 0) Cancel();
                return;
            }
            UpdateVisual();
            if (fired < waves && Time.time >= nextVolleyAt)
            {
                FireVolley();
                fired++;
                nextVolleyAt = Time.time + interval;
            }
        }

        private void FireVolley()
        {
            float phase = startAngle + fired * rotation;
            for (int i = 0; i < directions; i++)
            {
                float angle = (phase + i * 360f / directions) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                BossWitchProjectile projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
                projectile.transform.localScale = Vector3.one * scale;
                projectile.ConfigureFlight(speed, radius);
                projectile.Initialize(owner, direction, damage);
                projectiles.Add(projectile);
            }
        }

        private void UpdateVisual()
        {
            float elapsed = Mathf.Max(0f, Time.time - startedAt);
            int index = Mathf.FloorToInt(elapsed * Mathf.Max(1f, framesPerSecond)) % frames.Length;
            visual.sprite = frames[index];
            visual.enabled = true;
            float fade = Mathf.Min(Mathf.Clamp01(elapsed / 0.25f), Mathf.Clamp01((expiresAt - Time.time) / 0.25f));
            visual.color = new Color(1f, 0.8f, 0.4f, fade);
            transform.localScale = Vector3.one * (Mathf.Max(0.1f, visualScale) *
                Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(elapsed / 0.25f)) * (1f + Mathf.Sin(elapsed * 6f) * 0.06f));
        }

        public void Cancel()
        {
            initialized = false;
            foreach (BossWitchProjectile projectile in projectiles)
                if (projectile != null) Destroy(projectile.gameObject);
            projectiles.Clear();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            initialized = false;
            foreach (BossWitchProjectile projectile in projectiles)
                if (projectile != null) Destroy(projectile.gameObject);
            projectiles.Clear();
        }
    }
}
