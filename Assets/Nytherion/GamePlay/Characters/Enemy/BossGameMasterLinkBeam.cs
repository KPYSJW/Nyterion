using System.Collections.Generic;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossGameMasterLinkBeam : MonoBehaviour
    {
        private BossGameMasterEnemy owner;
        private Vector2 from, to, tip;
        private float width, launch, damage, damageInterval, animationSpeed, chargeDuration;
        private float phaseAt, firedAt, endRequestedAt;
        private Sprite[] starts, bodies, ends;
        private LineRenderer line;
        private SpriteRenderer start, end;
        private MaterialPropertyBlock properties;
        private Material capMaterial;
        private readonly Collider2D[] hits = new Collider2D[64];
        private readonly Dictionary<int, float> nextDamageAt = new Dictionary<int, float>();
        private bool initialized, endRequested;
        private enum Phase { Charging, Starting, Looping, Ending, Finished }
        private Phase phase = Phase.Finished;

        // ARCBIT frameTags and durations: charge 60ms, on 50ms, loop 70ms, off 60ms.
        private float OnDuration => Mathf.Max(launch, 3f * 0.05f / animationSpeed);
        private float OffDuration => 4f * 0.06f / animationSpeed;
        public bool IsFinished => !initialized || phase == Phase.Finished;
        public bool CanFire => initialized && phase == Phase.Charging && Time.time - phaseAt >= chargeDuration;

        public void Initialize(BossGameMasterEnemy source, Vector2 a, Vector2 b, float beamWidth,
            float launchTime, float warningTime, float hitDamage, float tickInterval, float playbackSpeed,
            Sprite[] startFrames, Sprite[] bodyFrames, Sprite[] endFrames, Material material, Material spriteMaterial, int layer, int order)
        {
            owner = source; from = a; to = b; tip = a;
            width = Mathf.Max(0.05f, beamWidth);
            animationSpeed = Mathf.Max(0.1f, playbackSpeed);
            chargeDuration = Mathf.Max(warningTime, 8f * 0.06f / animationSpeed);
            launch = Mathf.Max(0.01f, launchTime);
            damage = Mathf.Max(0f, hitDamage);
            damageInterval = Mathf.Max(0.05f, tickInterval);
            starts = startFrames; bodies = bodyFrames; ends = endFrames;
            capMaterial = spriteMaterial;
            phase = Phase.Charging;
            phaseAt = Time.time;
            endRequested = false;
            nextDamageAt.Clear();
            properties = new MaterialPropertyBlock();
            line = gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            // The union of the source body frames occupies 14 of 32 rows.
            line.widthMultiplier = width * 32f / 14f;
            line.startColor = line.endColor = Color.white;
            line.sortingLayerID = layer; line.sortingOrder = order;
            start = Cap("Laser Start", layer, order + 1);
            end = Cap("Laser End", layer, order + 1);
            initialized = true;
            Render();
        }

        public bool Fire()
        {
            if (!CanFire || endRequested || !Alive) return false;
            phase = Phase.Starting;
            phaseAt = firedAt = Time.time;
            Render();
            return true;
        }

        // Moving beams keep the same animation phase and per-target damage cooldowns.
        public void SetEndpoints(Vector2 a, Vector2 b)
        {
            if (!initialized) return;
            from = a;
            to = b;
            Render();
        }

        public void BeginEnd()
        {
            if (!initialized || endRequested) return;
            endRequested = true;
            endRequestedAt = Time.time;
            // Stop damage immediately, but finish the on frames before playing off if necessary.
            AdvancePhase(Time.time);
            if (initialized) Render();
        }

        private SpriteRenderer Cap(string label, int layer, int order)
        {
            GameObject cap = new GameObject(label);
            cap.transform.SetParent(transform, false);
            SpriteRenderer visual = cap.AddComponent<SpriteRenderer>();
            visual.sharedMaterial = capMaterial;
            visual.sortingLayerID = layer; visual.sortingOrder = order;
            return visual;
        }

        private bool Alive => owner != null && owner.isActiveAndEnabled && !owner.isDead && !owner.IsUpgrading;

        private void Update()
        {
            if (!initialized) return;
            if (!Alive) { Cancel(); return; }
            AdvancePhase(Time.time);
            if (!initialized) return;
            Render();
        }

        private void AdvancePhase(float now)
        {
            if (phase == Phase.Starting && now - phaseAt >= OnDuration)
            {
                phaseAt += OnDuration;
                phase = Phase.Looping;
            }
            if (endRequested && (phase == Phase.Looping || phase == Phase.Charging))
            {
                phaseAt = Mathf.Max(phaseAt, endRequestedAt);
                phase = Phase.Ending;
            }
            if (phase == Phase.Ending && now - phaseAt >= OffDuration) Cancel();
        }

        private static int FrameIndex(float elapsed, int first, int count, float frameSeconds, bool loop)
        {
            int index = Mathf.Max(0, Mathf.FloorToInt(elapsed / frameSeconds));
            return first + (loop ? index % count : Mathf.Min(index, count - 1));
        }

        private int BodyFrame(float elapsed)
        {
            if (phase == Phase.Starting) return FrameIndex(elapsed, 0, 3, 0.05f / animationSpeed, false);
            if (phase == Phase.Ending) return FrameIndex(elapsed, 9, 4, 0.06f / animationSpeed, false);
            return FrameIndex(elapsed, 3, 6, 0.07f / animationSpeed, true);
        }

        private int StartFrame(float elapsed)
        {
            if (phase == Phase.Charging) return FrameIndex(elapsed, 0, 8, 0.06f / animationSpeed, false);
            if (phase == Phase.Starting) return FrameIndex(elapsed, 8, 3, 0.05f / animationSpeed, false);
            if (phase == Phase.Ending) return FrameIndex(elapsed, 17, 4, 0.06f / animationSpeed, false);
            return FrameIndex(elapsed, 11, 6, 0.07f / animationSpeed, true);
        }

        private void Render()
        {
            float elapsed = Mathf.Max(0f, Time.time - phaseAt);
            bool charging = phase == Phase.Charging;
            tip = charging ? from : Vector2.Lerp(from, to, Mathf.Clamp01((Time.time - firedAt) / launch));
            line.enabled = end.enabled = !charging;
            line.SetPosition(0, from); line.SetPosition(1, tip);
            properties.Clear();
            properties.SetTexture("_MainTex", bodies[BodyFrame(elapsed)].texture);
            properties.SetFloat("_FrameCount", 1f);
            properties.SetFloat("_FrameIndex", 0f);
            properties.SetFloat("_Repeat", 1f);
            properties.SetFloat("_Scroll", 0f);
            line.SetPropertyBlock(properties);
            start.sprite = starts[StartFrame(elapsed)];
            end.sprite = ends[BodyFrame(elapsed)];
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            start.transform.SetPositionAndRotation(from, Quaternion.Euler(0, 0, angle));
            end.transform.SetPositionAndRotation(tip, Quaternion.Euler(0, 0, angle));
            start.transform.localScale = end.transform.localScale = Vector3.one * (width * 32f / 14f);
        }

        private void FixedUpdate()
        {
            if (!initialized || !Alive || endRequested || damage <= 0f ||
                (phase != Phase.Starting && phase != Phase.Looping)) return;
            // Recompute the currently revealed endpoint at physics time, never damage the unrevealed segment.
            Vector2 revealed = Vector2.Lerp(from, to, Mathf.Clamp01((Time.time - firedAt) / launch));
            float length = Vector2.Distance(from, revealed);
            if (length < 0.01f) return;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers); filter.useTriggers = true;
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            int count = Physics2D.OverlapBox((from + revealed) * 0.5f, new Vector2(length, width), angle, filter, hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;
                DecoyDummy dummy = hit.GetComponentInParent<DecoyDummy>();
                PlayerHealth player = dummy == null ? hit.GetComponentInParent<PlayerHealth>() : null;
                if (dummy == null && player == null) continue;
                if ((dummy != null && !dummy.IsAlive) || (player != null && player.IsInvulnerable)) continue;
                int id = dummy != null ? dummy.GetInstanceID() : player.GetInstanceID();
                if (nextDamageAt.TryGetValue(id, out float next) && Time.time < next) continue;
                if (EnemyAttackTarget.TryDamage(hit, damage, hit.ClosestPoint(revealed)))
                    nextDamageAt[id] = Time.time + damageInterval;
            }
        }

        public void Cancel()
        {
            if (!initialized) return;
            initialized = false;
            phase = Phase.Finished;
            nextDamageAt.Clear();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
