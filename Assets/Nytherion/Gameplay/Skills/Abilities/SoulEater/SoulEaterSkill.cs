using Nytherion.Core.Managers;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    public class SoulEaterSkill : SkillBase
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private string poolTag = "SoulEater";

        [Header("시전 이펙트")]
        [SerializeField] private Sprite[] castFrames;
        [SerializeField, Min(1f)] private float castFps = 14f;
        [SerializeField, Min(0.01f)] private float castVisualScale = 0.3f;
        private readonly List<CastEffect> castEffects = new List<CastEffect>();

        private sealed class CastEffect
        {
            public SpriteRenderer renderer;
            public float elapsed;
            public int spawnFrame;
        }
        
        [Header("Growth Settings")]
        [SerializeField] private float damageIncreaseOnKill = 1f;
        private float permanentBonusDamage = 0f;

        protected override void Activate()
        {
            if (skillData != null)
            {
                Vector3 spawnPosition = firePoint != null ? firePoint.position : caster.position;

                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mouseWorldPos.z = 0f;
                Vector3 direction = (mouseWorldPos - spawnPosition).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion spawnRotation = Quaternion.Euler(0f, 0f, angle);

                GameObject projectileInstance = ObjectPoolManager.Instance.SpawnFromPool(poolTag, spawnPosition, spawnRotation);

                if (projectileInstance != null && projectileInstance.TryGetComponent(out SoulEaterProj projectile))
                {
                    projectileInstance.SetActive(true);
                    projectileInstance.transform.localScale *= EffectSizeMultiplier;
                    
                    float totalDamage = ApplySkillDamage(skillData.damage + permanentBonusDamage);
                    
                    projectile.Initialize(totalDamage, speed, skillData.range, poolTag, OnEnemyKilled);
                    PlayCastEffect(spawnPosition, EffectSizeMultiplier, projectileInstance.GetComponent<SpriteRenderer>());
                }
                else
                {
                    Debug.LogError($"[SoulEaterSkill] 풀에서 오브젝트를 가져오지 못했거나 SoulEaterProj이 없습니다!");
                }
            }
        }

        private void PlayCastEffect(Vector3 origin, float size, SpriteRenderer projectileRenderer)
        {
            if (castFrames == null || castFrames.Length == 0 || castFrames[0] == null) return;
            CastEffect effect = castEffects.Find(item => item.renderer != null && !item.renderer.gameObject.activeSelf);
            if (effect == null)
            {
                GameObject effectObject = new GameObject("SoulEaterCastEffect");
                SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
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
            effect.renderer.transform.localScale = Vector3.one * (Mathf.Max(0.01f, size) * Mathf.Max(0.01f, castVisualScale));
            effect.renderer.sprite = castFrames[0];
            effect.elapsed = 0f;
            effect.spawnFrame = Time.frameCount;
            effect.renderer.gameObject.SetActive(true);
        }

        private void Update()
        {
            foreach (CastEffect effect in castEffects)
            {
                if (effect.renderer == null || !effect.renderer.gameObject.activeSelf || effect.spawnFrame == Time.frameCount) continue;
                if (castFrames == null || castFrames.Length == 0)
                {
                    effect.renderer.gameObject.SetActive(false);
                    continue;
                }
                effect.elapsed += Time.deltaTime;
                int frame = Mathf.FloorToInt(effect.elapsed * Mathf.Max(1f, castFps));
                if (frame >= castFrames.Length) effect.renderer.gameObject.SetActive(false);
                else effect.renderer.sprite = castFrames[frame];
            }
        }

        private void OnDisable()
        {
            foreach (CastEffect effect in castEffects)
                if (effect.renderer != null) effect.renderer.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (CastEffect effect in castEffects)
                if (effect.renderer != null) Destroy(effect.renderer.gameObject);
            castEffects.Clear();
        }

        private void OnEnemyKilled()
        {
            permanentBonusDamage += damageIncreaseOnKill;
            Debug.Log($"[SoulEaterSkill] 적 처치! 영구 데미지 증가. 현재 추가 데미지: {permanentBonusDamage}");
        }
    }
}
