using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>마우스 위치에 독방울을 떨어뜨려 착탄 시 범위 피해와 독을 부여합니다.</summary>
    public sealed class PoisonDropSkill : SkillBase
    {
        private sealed class Drop
        {
            public SpriteRenderer renderer;
            public PolygonCollider2D damageArea;
            public PoisonDropSkillData data;
            public readonly Vector2[] points = new Vector2[32];
            public float elapsed, damage;
            public int spawnFrame;
            public bool impacted;
            public Vector3 targetPosition;
        }

        private readonly List<Drop> drops = new List<Drop>();
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly HashSet<EnemyBase> enemies = new HashSet<EnemyBase>();

        public override bool CanUse() => base.CanUse() && isActiveAndEnabled &&
            skillData is PoisonDropSkillData data && data.HasValidFrames &&
            Camera.main != null && Mouse.current != null;

        protected override void Activate()
        {
            PoisonDropSkillData data = (PoisonDropSkillData)skillData;
            float groundZ = caster != null ? caster.position.z : transform.position.z;
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane ground = new Plane(Vector3.forward, new Vector3(0f, 0f, groundZ));
            Vector3 position = ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position;
            Drop drop = drops.Find(item => item.renderer != null && !item.renderer.gameObject.activeSelf);
            if (drop == null)
            {
                GameObject visual = new GameObject("PoisonDrop");
                visual.SetActive(false);
                drop = new Drop { renderer = visual.AddComponent<SpriteRenderer>(),
                    damageArea = visual.AddComponent<PolygonCollider2D>() };
                drop.renderer.sortingOrder = 10;
                drop.damageArea.isTrigger = true;
                drop.damageArea.enabled = false;
                drops.Add(drop);
            }
            int level = SkillOwner != null ? SkillOwner.GetSkillLevel(data) : Mathf.Max(1, data.skillLevel);
            float scale = Mathf.Max(0.01f, data.visualScale);
            drop.data = data;
            drop.elapsed = 0f;
            drop.damage = ApplySkillDamage(data.GetDamage(level));
            drop.spawnFrame = Time.frameCount;
            drop.impacted = false;
            drop.targetPosition = position;
            drop.renderer.transform.position = position + Vector3.up * Mathf.Max(0.01f, data.fallHeight);
            drop.renderer.transform.localScale = Vector3.one * (scale * EffectSizeMultiplier);
            drop.renderer.sprite = data.dropSprite;
            for (int i = 0; i < drop.points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / drop.points.Length;
                drop.points[i] = new Vector2(Mathf.Cos(angle) * Mathf.Max(0.01f, data.radius),
                    Mathf.Sin(angle) * Mathf.Max(0.01f, data.verticalRadius)) / scale;
            }
            drop.damageArea.SetPath(0, drop.points);
            drop.renderer.gameObject.SetActive(true);
        }

        private void Update()
        {
            foreach (Drop drop in drops)
            {
                if (drop.renderer == null || !drop.renderer.gameObject.activeSelf || drop.spawnFrame == Time.frameCount) continue;
                drop.elapsed += Time.deltaTime;
                PoisonDropSkillData data = drop.data;
                float fall = Mathf.Max(0.01f, data.fallDuration);
                float fps = Mathf.Max(1f, data.animationFps);
                if (drop.elapsed < fall)
                {
                    float progress = drop.elapsed / fall;
                    // 단일 독방울을 위에서 아래로 가속하며 목표 지점까지 이동합니다.
                    drop.renderer.transform.position = drop.targetPosition + Vector3.up *
                        (Mathf.Max(0.01f, data.fallHeight) * (1f - progress * progress));
                    continue;
                }
                if (!drop.impacted)
                {
                    // 큰 deltaTime에도 착탄 판정은 한 번만 발생합니다.
                    drop.impacted = true;
                    drop.renderer.transform.position = drop.targetPosition;
                    ApplyImpact(drop);
                }
                float activeTime = drop.elapsed - fall;
                int frame = Mathf.FloorToInt(activeTime * fps);
                if (frame >= data.frames.Length) drop.renderer.gameObject.SetActive(false);
                else drop.renderer.sprite = data.frames[frame];
            }
        }

        private void ApplyImpact(Drop drop)
        {
            hits.Clear();
            enemies.Clear();
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(drop.data.targetLayers);
            drop.damageArea.enabled = true;
            try
            {
                Physics2D.SyncTransforms();
                drop.damageArea.OverlapCollider(filter, hits);
            }
            finally { drop.damageArea.enabled = false; }
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.isDead || !enemies.Add(enemy)) continue;
                enemy.TakeDamage(drop.damage);
                if (!enemy.isDead && enemy.isActiveAndEnabled && enemy.TryGetComponent(out StatusEffectManager status))
                    status.ApplyEffect(new PoisonEffect(drop.damage * Mathf.Max(0f, drop.data.poisonDamageRatio),
                        Mathf.Max(0.01f, drop.data.poisonDuration)));
            }
        }

        private void OnDisable()
        {
            foreach (Drop drop in drops) if (drop.renderer != null) drop.renderer.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (Drop drop in drops) if (drop.renderer != null) Destroy(drop.renderer.gameObject);
        }
    }
}
