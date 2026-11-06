using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossGameMasterHomingProjectile : MonoBehaviour
    {
        private BossGameMasterEnemy owner;
        private EnemyAIController ai;
        private SpriteRenderer visual;
        private Sprite[] frames;
        private Sprite[] impactFrames;
        private LayerMask wallLayers;
        private Vector2 direction;
        private float speed, turnSpeed, trackingDuration, lifetime, radius, damage, fps, angleOffset;
        private float bornAt, releasedAt;
        private bool initialized, released;
        private bool impacting;
        private float impactAt, impactFps, impactScale;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[64];
        public bool IsFinished => !initialized;
        public bool IsReleased => released;
        private bool Alive => owner != null && owner.isActiveAndEnabled && !owner.isDead && !owner.IsUpgrading;

        public void Initialize(BossGameMasterEnemy source, EnemyAIController controller, float moveSpeed, float degreesPerSecond,
            float trackingSeconds, float flightSeconds, float scale, float hitRadius, float hitDamage, float animationFps,
            float spriteAngleOffset, LayerMask walls, Sprite[] sprites, Material material, int layer, int order)
        {
            owner = source; ai = controller; frames = sprites; wallLayers = walls;
            speed = Mathf.Max(0.1f, moveSpeed);
            turnSpeed = Mathf.Max(0f, degreesPerSecond);
            trackingDuration = Mathf.Max(0f, trackingSeconds);
            lifetime = Mathf.Max(trackingDuration + 0.1f, flightSeconds);
            radius = Mathf.Max(0.01f, hitRadius) * Mathf.Max(0.1f, scale);
            damage = Mathf.Max(0f, hitDamage); fps = Mathf.Max(1f, animationFps);
            angleOffset = spriteAngleOffset;
            transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
            visual = gameObject.AddComponent<SpriteRenderer>();
            visual.sharedMaterial = material; visual.sortingLayerID = layer; visual.sortingOrder = order;
            visual.sprite = frames[0];
            bornAt = Time.time;
            released = false; initialized = true;
            impacting = false;
        }

        public void ConfigureImpact(Sprite[] sprites, float animationFps, float scale)
        {
            impactFrames = sprites;
            impactFps = Mathf.Max(1f, animationFps);
            impactScale = Mathf.Max(0.1f, scale);
        }

        public void SetWaitingPosition(Vector2 point, Vector2 facing)
        {
            if (!initialized || released) return;
            transform.position = new Vector3(point.x, point.y, owner.transform.position.z);
            direction = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.right;
            FaceDirection();
        }

        public void Fire()
        {
            if (!initialized || released) return;
            if (!Alive) { Cancel(); return; }
            if (ai != null && ai.CurrentTarget != null)
            {
                Vector2 aim = ai.CurrentTarget.position - transform.position;
                if (aim.sqrMagnitude > 0.0001f) direction = aim.normalized;
            }
            FireInDirection(direction);
        }

        public void FireInDirection(Vector2 aim)
        {
            if (!initialized || released) return;
            if (!Alive) { Cancel(); return; }
            direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            releasedAt = Time.time;
            released = true;
            FaceDirection();
        }

        private void FaceDirection() => transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + angleOffset);

        private void Update()
        {
            if (!initialized) return;
            if (!Alive) { Cancel(); return; }
            if (impacting)
            {
                int index = Mathf.FloorToInt((Time.time - impactAt) * impactFps);
                if (index >= impactFrames.Length) { Cancel(); return; }
                visual.sprite = impactFrames[Mathf.Max(0, index)];
                return;
            }
            if (released && Time.time - releasedAt >= lifetime) { Cancel(); return; }
            visual.sprite = frames[Mathf.FloorToInt((Time.time - bornAt) * fps) % frames.Length];
        }

        private bool IsWall(Collider2D hit) => hit.CompareTag(Tags.Wall) || (wallLayers.value & (1 << hit.gameObject.layer)) != 0;

        private bool TryHit(Collider2D hit, Vector2 point)
        {
            if (damage <= 0f || hit == null || hit.transform.IsChildOf(owner.transform)) return false;
            DecoyDummy dummy = hit.GetComponentInParent<DecoyDummy>();
            PlayerHealth player = dummy == null ? hit.GetComponentInParent<PlayerHealth>() : null;
            if (dummy == null && player == null) return false;
            if ((dummy != null && !dummy.IsAlive) || (player != null && player.IsInvulnerable)) return false;
            return EnemyAttackTarget.TryDamage(hit, damage, point);
        }

        private void FixedUpdate()
        {
            if (!initialized || !released || impacting) return;
            if (!Alive || Time.time - releasedAt >= lifetime) { Cancel(); return; }
            if (Time.time - releasedAt < trackingDuration && ai != null && ai.CurrentTarget != null)
            {
                Vector2 aim = ai.CurrentTarget.position - transform.position;
                if (aim.sqrMagnitude > 0.0001f)
                {
                    float current = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    float target = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                    float angle = Mathf.MoveTowardsAngle(current, target, turnSpeed * Time.fixedDeltaTime) * Mathf.Deg2Rad;
                    direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }
            FaceDirection();
            Vector2 origin = transform.position;
            float distance = speed * Time.fixedDeltaTime;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers); filter.useTriggers = true;
            int count = Physics2D.CircleCast(origin, radius, direction, filter, hits, distance);
            float wallDistance = float.PositiveInfinity;
            Vector2 wallPoint = origin;
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && !hits[i].collider.transform.IsChildOf(owner.transform) && IsWall(hits[i].collider) &&
                    hits[i].distance < wallDistance)
                {
                    wallDistance = hits[i].distance;
                    wallPoint = hits[i].point;
                }
            // CircleCast results are ordered by distance. Walls take precedence over overlapping targets behind them.
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && hits[i].distance < wallDistance && !IsWall(hits[i].collider) &&
                    TryHit(hits[i].collider, hits[i].point)) { BeginImpact(hits[i].point); return; }
            if (wallDistance <= distance) { BeginImpact(wallPoint); return; }
            Vector2 next = origin + direction * distance;
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        private void BeginImpact(Vector2 point)
        {
            if (impactFrames == null || impactFrames.Length == 0) { Cancel(); return; }
            impacting = true;
            impactAt = Time.time;
            transform.position = new Vector3(point.x, point.y, transform.position.z);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * impactScale;
            visual.sprite = impactFrames[0];
        }

        public void Cancel()
        {
            if (!initialized) return;
            initialized = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
