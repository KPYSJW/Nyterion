using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>마우스 위치에 고정된 화염 폭풍을 생성하고 주기적으로 범위 피해를 줍니다.</summary>
    public sealed class FireStormSkill : SkillBase
    {
        private sealed class Storm
        {
            public SpriteRenderer renderer;
            public PolygonCollider2D damageArea;
            public readonly Vector2[] areaPoints = new Vector2[32];
            public FireStormSkillData data;
            public float elapsed, nextTick, damage, size;
            public int spawnFrame;
        }

        private sealed class HitVisual
        {
            public SpriteRenderer renderer;
            public FireStormSkillData data;
            public float elapsed;
            public int spawnFrame;
        }

        private readonly List<Storm> storms = new List<Storm>();
        private readonly List<HitVisual> hitVisuals = new List<HitVisual>();
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly HashSet<EnemyBase> enemies = new HashSet<EnemyBase>();

        public override bool CanUse() => base.CanUse() && isActiveAndEnabled &&
            skillData is FireStormSkillData data && data.HasValidFrames &&
            Camera.main != null && Mouse.current != null;

        protected override void Activate()
        {
            FireStormSkillData data = (FireStormSkillData)skillData;
            float groundZ = caster != null ? caster.position.z : transform.position.z;
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane ground = new Plane(Vector3.forward, new Vector3(0f, 0f, groundZ));
            // 일반적인 2D 카메라의 지면 평면에 마우스 좌표를 투영합니다.
            Vector3 position = ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position;
            Storm storm = storms.Find(item => item.renderer != null && !item.renderer.gameObject.activeSelf);
            if (storm == null)
            {
                storm = new Storm { renderer = CreateRenderer("FireStorm", 10) };
                storm.damageArea = storm.renderer.gameObject.AddComponent<PolygonCollider2D>();
                storm.damageArea.isTrigger = true;
                storm.damageArea.enabled = false;
                storms.Add(storm);
            }
            int level = SkillOwner != null ? SkillOwner.GetSkillLevel(data) : Mathf.Max(1, data.skillLevel);
            storm.data = data;
            storm.elapsed = 0f;
            storm.nextTick = 0f;
            storm.damage = ApplySkillDamage(data.GetDamage(level));
            storm.size = EffectSizeMultiplier;
            storm.spawnFrame = Time.frameCount;
            storm.renderer.transform.position = position;
            storm.renderer.transform.localScale = Vector3.one * (data.visualScale * storm.size);
            storm.renderer.sprite = data.spawnFrames[0];
            ConfigureDamageArea(storm);
            storm.renderer.gameObject.SetActive(true);
        }

        private static void ConfigureDamageArea(Storm storm)
        {
            // 표시용 스케일을 제외하고 판정에는 플레이어의 크기 배율만 적용합니다.
            float visualScale = Mathf.Max(0.01f, storm.data.visualScale);
            Vector2 offset = storm.data.damageOffset / visualScale;
            for (int i = 0; i < storm.areaPoints.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / storm.areaPoints.Length;
                storm.areaPoints[i] = offset + new Vector2(
                    Mathf.Cos(angle) * Mathf.Max(0.01f, storm.data.radius),
                    Mathf.Sin(angle) * Mathf.Max(0.01f, storm.data.verticalRadius)) / visualScale;
            }
            storm.damageArea.SetPath(0, storm.areaPoints);
        }

        private static SpriteRenderer CreateRenderer(string name, int order)
        {
            SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            renderer.gameObject.SetActive(false);
            return renderer;
        }

        private void Update()
        {
            foreach (Storm storm in storms)
            {
                if (storm.renderer == null || !storm.renderer.gameObject.activeSelf || storm.spawnFrame == Time.frameCount) continue;
                FireStormSkillData data = storm.data;
                storm.elapsed += Time.deltaTime;
                float spawnDuration = data.spawnFrames.Length / Mathf.Max(1f, data.spawnFps);
                if (storm.elapsed < spawnDuration)
                {
                    storm.renderer.sprite = data.spawnFrames[Mathf.FloorToInt(storm.elapsed * Mathf.Max(1f, data.spawnFps))];
                    continue;
                }
                float activeTime = storm.elapsed - spawnDuration;
                float duration = Mathf.Max(0.01f, data.duration);
                // 생성 완료 직후부터 피해를 주며, 종료 시각의 추가 타격은 제외합니다.
                while (storm.nextTick <= activeTime && storm.nextTick < duration)
                {
                    ApplyTick(storm);
                    storm.nextTick += Mathf.Max(0.01f, data.tickInterval);
                }
                if (activeTime >= duration) { storm.renderer.gameObject.SetActive(false); continue; }
                storm.renderer.sprite = data.loopFrames[Mathf.FloorToInt(activeTime * Mathf.Max(1f, data.loopFps)) % data.loopFrames.Length];
            }
            foreach (HitVisual hit in hitVisuals)
            {
                if (hit.renderer == null || !hit.renderer.gameObject.activeSelf || hit.spawnFrame == Time.frameCount) continue;
                hit.elapsed += Time.deltaTime;
                int frame = Mathf.FloorToInt(hit.elapsed * Mathf.Max(1f, hit.data.hitFps));
                if (frame >= hit.data.hitFrames.Length) hit.renderer.gameObject.SetActive(false);
                else hit.renderer.sprite = hit.data.hitFrames[frame];
            }
        }

        private void ApplyTick(Storm storm)
        {
            hits.Clear();
            enemies.Clear();
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(storm.data.targetLayers);
            // 타원을 32각형으로 근사해 적 콜라이더와 실제로 겹치는지 검사합니다.
            // 쿼리 동안만 활성화하여 일반 물리 충돌이나 트리거 이벤트에 참여하지 않습니다.
            storm.damageArea.enabled = true;
            try
            {
                Physics2D.SyncTransforms();
                storm.damageArea.OverlapCollider(filter, hits);
            }
            finally { storm.damageArea.enabled = false; }
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.isDead || !enemies.Add(enemy)) continue;
                Vector3 position = enemy.TryGetGroundHitBounds(out Bounds bounds) ? bounds.center : enemy.transform.position;
                enemy.TakeDamage(storm.damage);
                if (!enemy.isDead && enemy.isActiveAndEnabled && enemy.TryGetComponent(out StatusEffectManager status))
                    status.ApplyEffect(new FireEffect(storm.damage * Mathf.Max(0f, storm.data.burnDamageRatio), storm.data.burnDuration));
                PlayHit(storm, position);
            }
        }

        private void PlayHit(Storm storm, Vector3 position)
        {
            HitVisual hit = hitVisuals.Find(item => item.renderer != null && !item.renderer.gameObject.activeSelf);
            if (hit == null)
            {
                hit = new HitVisual { renderer = CreateRenderer("FireStormHit", 20) };
                hitVisuals.Add(hit);
            }
            hit.data = storm.data;
            hit.elapsed = 0f;
            hit.spawnFrame = Time.frameCount;
            hit.renderer.transform.position = position;
            hit.renderer.transform.localScale = Vector3.one * (storm.size * storm.data.hitVisualScale);
            hit.renderer.sprite = storm.data.hitFrames[0];
            hit.renderer.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            foreach (Storm storm in storms) if (storm.renderer != null) storm.renderer.gameObject.SetActive(false);
            foreach (HitVisual hit in hitVisuals) if (hit.renderer != null) hit.renderer.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (Storm storm in storms) if (storm.renderer != null) Destroy(storm.renderer.gameObject);
            foreach (HitVisual hit in hitVisuals) if (hit.renderer != null) Destroy(hit.renderer.gameObject);
        }
    }
}
