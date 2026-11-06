using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Skills
{
    public class ThunderboltSkill : SkillBase
    {
        private readonly List<ThunderboltProjectile> projectiles = new List<ThunderboltProjectile>();
        private readonly List<CastEffect> castEffects = new List<CastEffect>();
        private readonly List<RaycastHit2D> launchHits = new List<RaycastHit2D>();

        private sealed class CastEffect
        {
            public SpriteRenderer renderer;
            public float elapsed;
            public int spawnFrame;
        }

        public override bool CanUse() => base.CanUse() && isActiveAndEnabled &&
            skillData is ThunderboltSkillData data && data.projectilePrefab != null && data.HasValidFrames;

        protected override void Activate()
        {
            ThunderboltSkillData data = (ThunderboltSkillData)skillData;
            Vector3 origin = caster != null ? caster.position : (firePoint != null ? firePoint.position : transform.position);
            Vector2 direction = firePoint != null ? (Vector2)firePoint.right : Vector2.right;
            Camera camera = Camera.main;
            if (camera != null && Mouse.current != null)
            {
                Vector2 screen = Mouse.current.position.ReadValue();
                Vector3 mouse = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(camera.transform.position.z - origin.z)));
                direction = (Vector2)(mouse - origin);
            }
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();
            var player = SkillOwner;
            int level = player != null ? player.GetSkillLevel(data) : Mathf.Max(1, data.skillLevel);
            PlayerSkillManager skills = caster != null ? caster.GetComponentInParent<PlayerSkillManager>() :
                GetComponentInParent<PlayerSkillManager>();
            int extra = player != null && player.currentPlayerData != null
                ? Mathf.Max(0, Mathf.FloorToInt(player.currentPlayerData.extraProjectiles)) : 0;
            int count = 1 + extra;
            float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float damage = ApplySkillDamage(data.GetDamage(level));
            float size = EffectSizeMultiplier;
            origin = GetLaunchPosition(data, origin, direction, size);
            PlayCastEffect(data, origin, size);
            float spreadAngle = Mathf.Clamp(data.projectileSpreadAngle, 0f, 180f);
            for (int i = 0; i < count; i++)
            {
                ThunderboltProjectile projectile = projectiles.Find(item => item != null && !item.gameObject.activeSelf);
                if (projectile == null)
                {
                    projectile = Instantiate(data.projectilePrefab);
                    projectiles.Add(projectile);
                }
                float offset = count == 1 ? 0f : Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, i / (float)(count - 1));
                float radians = (centerAngle + offset) * Mathf.Deg2Rad;
                Vector2 launchDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                projectile.Begin(data, origin, launchDirection, damage, size,
                    skills != null && skills.DebugThunderboltHoming,
                    skills != null ? skills.DebugFireballHomingTurnSpeed : 180f,
                    skills != null ? skills.DebugFireballHomingStraightTime : 0f);
            }
        }

        private Vector3 GetLaunchPosition(ThunderboltSkillData data, Vector3 origin, Vector2 direction, float size)
        {
            float distance = Mathf.Max(0f, data.launchOffset);
            if (distance <= 0f) return origin;

            // 발사 위치를 앞당길 때 가까운 적이나 벽을 건너뛰지 않도록 제한합니다.
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(data.collisionLayers);
            launchHits.Clear();
            Physics2D.CircleCast(origin, data.collisionRadius * Mathf.Max(0.01f, size), direction, filter, launchHits, distance);
            foreach (RaycastHit2D hit in launchHits)
                if (hit.collider != null) distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.001f));
            return origin + (Vector3)(direction * distance);
        }

        private void PlayCastEffect(ThunderboltSkillData data, Vector3 origin, float size)
        {
            if (data.castFrames == null || data.castFrames.Length == 0 || data.castFrames[0] == null) return;

            CastEffect effect = castEffects.Find(item => item.renderer != null && !item.renderer.gameObject.activeSelf);
            if (effect == null)
            {
                GameObject effectObject = new GameObject("ThunderboltCastEffect");
                SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
                SpriteRenderer projectileRenderer = data.projectilePrefab.GetComponent<SpriteRenderer>();
                if (projectileRenderer != null)
                {
                    renderer.sharedMaterial = projectileRenderer.sharedMaterial;
                    renderer.sortingLayerID = projectileRenderer.sortingLayerID;
                    renderer.sortingOrder = projectileRenderer.sortingOrder - 1;
                }
                effect = new CastEffect { renderer = renderer };
                castEffects.Add(effect);
            }

            effect.renderer.transform.position = origin;
            effect.renderer.transform.localScale = Vector3.one * (Mathf.Max(0.01f, size) * Mathf.Max(0.01f, data.castVisualScale));
            effect.renderer.sprite = data.castFrames[0];
            effect.elapsed = 0f;
            effect.spawnFrame = Time.frameCount;
            effect.renderer.gameObject.SetActive(true);
        }

        private void Update()
        {
            ThunderboltSkillData data = skillData as ThunderboltSkillData;
            foreach (CastEffect effect in castEffects)
            {
                if (effect.renderer == null || !effect.renderer.gameObject.activeSelf || effect.spawnFrame == Time.frameCount) continue;
                if (data == null || data.castFrames == null || data.castFrames.Length == 0)
                {
                    effect.renderer.gameObject.SetActive(false);
                    continue;
                }

                effect.elapsed += Time.deltaTime;
                int frame = Mathf.FloorToInt(effect.elapsed * Mathf.Max(1f, data.castFps));
                if (frame >= data.castFrames.Length) effect.renderer.gameObject.SetActive(false);
                else effect.renderer.sprite = data.castFrames[frame];
            }
        }

        private void OnDisable()
        {
            foreach (ThunderboltProjectile projectile in projectiles)
                if (projectile != null) projectile.gameObject.SetActive(false);
            foreach (CastEffect effect in castEffects)
                if (effect.renderer != null) effect.renderer.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (ThunderboltProjectile projectile in projectiles)
                if (projectile != null) Destroy(projectile.gameObject);
            projectiles.Clear();
            foreach (CastEffect effect in castEffects)
                if (effect.renderer != null) Destroy(effect.renderer.gameObject);
            castEffects.Clear();
        }
    }
}
