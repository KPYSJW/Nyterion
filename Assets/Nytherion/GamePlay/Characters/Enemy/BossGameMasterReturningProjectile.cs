using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossGameMasterReturningProjectile : MonoBehaviour
    {
        private enum Phase { OutwardStart, OutwardLoop, OutwardStop, ReturnStart, ReturnLoop, ReturnStop, Finished }
        private Phase phase = Phase.Finished;
        private BossGameMasterEnemy owner;
        private SpriteRenderer visual;
        private Sprite[] frames;
        private Vector2 direction, outwardDirection, target;
        private float outwardSpeed, returnSpeed, radius, damage, fps, phaseAt, began, lifetime;
        private bool outwardArrived, returnArrived, initialized;
        private LayerMask wallLayers;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[64];
        private readonly Collider2D[] overlaps = new Collider2D[64];
        private readonly HashSet<int> damaged = new HashSet<int>();
        private float StartDuration => 5f / fps;
        public bool IsFinished => !initialized || phase == Phase.Finished;
        private bool Alive => owner != null && owner.isActiveAndEnabled && !owner.isDead && !owner.IsUpgrading;
        private bool Returning => phase == Phase.ReturnStart || phase == Phase.ReturnLoop || phase == Phase.ReturnStop;

        public void Initialize(BossGameMasterEnemy source, Vector3 origin, Vector2 aim,
            float distance, float outSpeed, float backSpeed, float scale, float baseRadius, float hitDamage,
            float framesPerSecond, LayerMask walls, Sprite[] sprites, Material material, int layer, int order)
        {
            owner = source;
            transform.position = origin;
            transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
            direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            outwardDirection = direction;
            target = (Vector2)origin + direction * Mathf.Max(0.1f, distance);
            outwardSpeed = Mathf.Max(0.1f, outSpeed);
            returnSpeed = Mathf.Max(0.1f, backSpeed);
            radius = Mathf.Max(0.01f, baseRadius) * Mathf.Max(0.1f, scale);
            damage = Mathf.Max(0f, hitDamage);
            fps = Mathf.Max(1f, framesPerSecond);
            wallLayers = walls;
            frames = sprites;
            visual = GetComponent<SpriteRenderer>();
            if (visual == null) visual = gameObject.AddComponent<SpriteRenderer>();
            visual.sharedMaterial = material;
            visual.sortingLayerID = layer;
            visual.sortingOrder = order;
            visual.color = Color.white;
            outwardArrived = returnArrived = false;
            damaged.Clear();
            phase = Phase.OutwardStart;
            phaseAt = began = Time.time;
            lifetime = Mathf.Max(30f, Mathf.Max(0.1f, distance) / outwardSpeed + 4f * StartDuration + 5f);
            initialized = true;
            FaceDirection();
            Render();
        }

        private void Update()
        {
            if (!initialized) return;
            if (!Alive || Time.time - began > lifetime) { Cancel(); return; }
            AdvancePhase(Time.time);
            if (initialized) Render();
        }

        private void AdvancePhase(float now)
        {
            for (int i = 0; i < 4 && initialized; i++)
            {
                if (now - phaseAt < StartDuration) break;
                if (phase == Phase.OutwardStart)
                {
                    phaseAt += StartDuration;
                    phase = outwardArrived ? Phase.OutwardStop : Phase.OutwardLoop;
                }
                else if (phase == Phase.OutwardStop)
                {
                    // Reverse 4 -> 0 has finished: return immediately, with no stationary wait stage.
                    phaseAt += StartDuration;
                    phase = Phase.ReturnStart;
                    damaged.Clear();
                    direction = -outwardDirection;
                    FaceDirection();
                    ExtendReturnLifetime();
                }
                else if (phase == Phase.ReturnStart)
                {
                    phaseAt += StartDuration;
                    phase = returnArrived ? Phase.ReturnStop : Phase.ReturnLoop;
                }
                else if (phase == Phase.ReturnStop) Cancel();
                else break;
            }
        }

        private void Render()
        {
            int frame = Mathf.Max(0, Mathf.FloorToInt((Time.time - phaseAt) * fps));
            if (phase == Phase.OutwardLoop || phase == Phase.ReturnLoop) frame = 5 + frame % (frames.Length - 5);
            else if (phase == Phase.OutwardStop || phase == Phase.ReturnStop) frame = 4 - Mathf.Min(frame, 4);
            else frame = Mathf.Min(frame, 4);
            visual.sprite = frames[frame];
        }

        private void FaceDirection()
        {
            // Source frames face right. Rotate the entire head/tail so the tail stays behind motion.
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void Arrive()
        {
            if (Returning)
            {
                returnArrived = true;
                if (phase == Phase.ReturnLoop) { phase = Phase.ReturnStop; phaseAt = Time.time; }
            }
            else
            {
                outwardArrived = true;
                if (phase == Phase.OutwardLoop) { phase = Phase.OutwardStop; phaseAt = Time.time; }
            }
            Render();
        }

        private bool IsWall(Collider2D hit) => hit.CompareTag(Tags.Wall) || (wallLayers.value & (1 << hit.gameObject.layer)) != 0;

        private void ExtendReturnLifetime()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(wallLayers);
            filter.useTriggers = true;
            int count = Physics2D.CircleCast(transform.position, radius, direction, filter, hits, float.PositiveInfinity);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && IsWall(hits[i].collider) && !hits[i].collider.transform.IsChildOf(owner.transform))
                    nearest = Mathf.Min(nearest, hits[i].distance);
            // Account for large rooms; the safety timeout must not cut a valid return short of its wall.
            if (!float.IsPositiveInfinity(nearest))
                lifetime = Mathf.Max(lifetime, Time.time - began + nearest / returnSpeed + 2f * StartDuration + 1f);
        }

        private void Damage(Collider2D hit, Vector2 point)
        {
            if (hit == null || hit.transform.IsChildOf(owner.transform) || damage <= 0f) return;
            DecoyDummy dummy = hit.GetComponentInParent<DecoyDummy>();
            PlayerHealth player = dummy == null ? hit.GetComponentInParent<PlayerHealth>() : null;
            if (dummy == null && player == null) return;
            if ((dummy != null && !dummy.IsAlive) || (player != null && player.IsInvulnerable)) return;
            int id = dummy != null ? dummy.GetInstanceID() : player.GetInstanceID();
            if (!damaged.Contains(id) && EnemyAttackTarget.TryDamage(hit, damage, point)) damaged.Add(id);
        }

        private void FixedUpdate()
        {
            if (!initialized) return;
            if (!Alive) { Cancel(); return; }
            AdvancePhase(Time.time);
            if (!initialized) return;
            if (phase == Phase.ReturnStop) return;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            Vector2 origin = transform.position;
            if (phase == Phase.OutwardStop || (!Returning && outwardArrived) || (Returning && returnArrived))
            {
                int count = Physics2D.OverlapCircle(origin, radius, filter, overlaps);
                for (int i = 0; i < count; i++) Damage(overlaps[i], overlaps[i].ClosestPoint(origin));
                return;
            }
            float remaining = float.PositiveInfinity;
            if (!Returning)
            {
                Vector2 delta = target - origin;
                remaining = delta.magnitude;
                if (remaining < 0.01f) { Arrive(); return; }
                direction = delta / remaining;
            }
            FaceDirection();
            float distance = Mathf.Min(remaining, (Returning ? returnSpeed : outwardSpeed) * Time.fixedDeltaTime);
            int hitCount = Physics2D.CircleCast(origin, radius, direction, filter, hits, distance);
            float wallDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = hits[i].collider;
                if (hit == null || hit.transform.IsChildOf(owner.transform) || !IsWall(hit)) continue;
                wallDistance = Mathf.Min(wallDistance, hits[i].distance);
            }
            for (int i = 0; i < hitCount; i++)
                if (hits[i].collider != null && hits[i].distance < wallDistance && !IsWall(hits[i].collider))
                    Damage(hits[i].collider, hits[i].point);
            bool blocked = wallDistance <= distance;
            float travel = blocked ? Mathf.Max(0f, wallDistance - 0.02f) : distance;
            Vector2 next = origin + direction * travel;
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            if (blocked || (!Returning && distance >= remaining)) Arrive();
        }

        public void Cancel()
        {
            if (!initialized) return;
            initialized = false;
            phase = Phase.Finished;
            damaged.Clear();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
