using System.Collections.Generic;
using Nytherion.Core.Enums;
using UnityEngine;

namespace Nytherion.GamePlay.Relics
{
    /// <summary>
    /// 고등급 유물이 드롭되는 동안 유물 이미지를 가리는 빛 덩어리 연출이다.
    /// 별도 스프라이트 시트 없이 런타임 그라디언트 스프라이트를 생성해 사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RarityWorldLightEffect : MonoBehaviour
    {
        private const int AmbientParticleCount = 10;

        private static Sprite glowSprite;

        private Transform effectRoot;
        private SpriteRenderer sourceRenderer;
        private SpriteRenderer coreRenderer;
        private SpriteRenderer haloRenderer;
        private Sprite coreSprite;
        private Sprite ambientParticleSprite;
        private RuntimeAnimatorController ambientParticleController;
        private float ambientParticleAnimationDuration = 0.7f;
        private readonly List<AmbientParticle> ambientParticles = new List<AmbientParticle>();

        private bool isPlaying;
        private bool isRevealing;
        private bool isAmbient;
        private bool isLandingBurst;
        private bool showAmbientParticles;
        private float revealProgress;
        private float intensity;
        private Color effectColor;
        private float ambientParticleSpeed = 0.75f;
        private float ambientParticleStartDistance;
        private float ambientParticleEndDistance = 1.35f;
        private float ambientParticleRepeatDelay = 0.12f;
        private float ambientParticleTravelDuration = 0.7f;
        private float ambientParticleVisualScale = 20f;

        public void ConfigureAmbientParticleSettings(
            float speed,
            float startDistance,
            float endDistance,
            float repeatDelay,
            float travelDuration,
            float visualScale)
        {
            ambientParticleSpeed = Mathf.Max(0.01f, speed);
            ambientParticleStartDistance = Mathf.Max(0f, startDistance);
            ambientParticleEndDistance = Mathf.Max(ambientParticleStartDistance, endDistance);
            ambientParticleRepeatDelay = Mathf.Max(0f, repeatDelay);
            ambientParticleTravelDuration = Mathf.Max(0.05f, travelDuration);
            ambientParticleVisualScale = Mathf.Max(0.1f, visualScale);
        }

        public void BeginConceal(
            Rarity rarity,
            SpriteRenderer relicRenderer,
            Sprite centerLightSprite,
            Sprite particleSprite)
        {
            sourceRenderer = relicRenderer;
            coreSprite = centerLightSprite;
            ambientParticleSprite = particleSprite;
            ambientParticleController = null;

            if (sourceRenderer == null)
            {
                Clear();
                return;
            }

            EnsureVisuals();
            if (effectRoot == null) return;

            coreRenderer.sprite = coreSprite != null ? coreSprite : GetGlowSprite();
            isPlaying = true;
            isRevealing = false;
            isAmbient = false;
            isLandingBurst = false;
            showAmbientParticles = rarity >= Rarity.Rare;
            revealProgress = 0f;
            intensity = GetIntensity(rarity);
            effectColor = GetColor(rarity);
            InitializeAmbientParticles(rarity);
            effectRoot.gameObject.SetActive(true);
        }

        public void BeginLandingBurst()
        {
            if (!isPlaying)
            {
                return;
            }

            isLandingBurst = true;
        }

        /// <summary>
        /// 0은 빛 덩어리만 보이는 상태, 1은 빛이 완전히 사라진 상태다.
        /// </summary>
        public void SetRevealProgress(float progress)
        {
            if (!isPlaying) return;

            isRevealing = true;
            revealProgress = Mathf.Clamp01(progress);

            if (revealProgress >= 1f)
            {
                isRevealing = false;
                isLandingBurst = false;

                if (showAmbientParticles)
                {
                    isAmbient = true;
                    BeginAmbientParticleEmission();
                }
                else
                {
                    Clear();
                }
            }
        }

        public void Clear()
        {
            isPlaying = false;
            isRevealing = false;
            isAmbient = false;
            isLandingBurst = false;
            showAmbientParticles = false;
            revealProgress = 0f;

            if (effectRoot != null)
            {
                effectRoot.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Update()
        {
            if (!isPlaying || effectRoot == null || sourceRenderer == null) return;

            float baseRadius = GetBaseRadius();

            if (isAmbient)
            {
                coreRenderer.color = Color.clear;
                haloRenderer.color = Color.clear;
                UpdateAmbientParticles(baseRadius);
                return;
            }

            float pulse = 0.96f + Mathf.Sin(Time.time * 9f) * 0.04f;
            float scale = isRevealing
                ? Mathf.Lerp(1.42f, 0.12f, revealProgress)
                : isLandingBurst ? 1.42f : 1f;
            float coreOpacity = 1f;

            SetRenderer(coreRenderer, Vector2.one * baseRadius * 1.75f * intensity * pulse * scale,
                WithAlpha(effectColor, coreOpacity));
            SetRenderer(haloRenderer, Vector2.zero, Color.clear);
        }

        private void EnsureVisuals()
        {
            if (effectRoot != null) return;

            GameObject rootObject = new GameObject("RarityWorldLightEffect");
            rootObject.layer = gameObject.layer;
            effectRoot = rootObject.transform;
            effectRoot.SetParent(transform, false);

            haloRenderer = CreateRenderer("Halo");
            coreRenderer = CreateRenderer("Core");

            for (int i = 0; i < AmbientParticleCount; i++)
            {
                SpriteRenderer particleRenderer = CreateRenderer($"AmbientParticle_{i + 1}");
                particleRenderer.sortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder - 1 : -1;
                ambientParticles.Add(new AmbientParticle { renderer = particleRenderer });
            }

            effectRoot.gameObject.SetActive(false);
        }

        private void InitializeAmbientParticles(Rarity rarity)
        {
            int activeParticleCount = rarity == Rarity.Legendary ? 10 : rarity == Rarity.Epic ? 8 : 6;

            for (int i = 0; i < ambientParticles.Count; i++)
            {
                AmbientParticle particle = ambientParticles[i];
                particle.isActive = i < activeParticleCount;
                particle.angle = Random.Range(0f, 360f);
                particle.phase = Random.Range(0f, 1f);
                particle.speed = Random.Range(0.8f, 1.15f) * ambientParticleSpeed;
                particle.radiusMultiplier = Random.Range(0.9f, 1.1f);
                particle.sizeMultiplier = Random.Range(0.2f, 0.32f) * intensity;
                particle.rotationSpeed = Random.Range(-140f, 140f);
                particle.isPlayingOneShot = false;

                if (particle.renderer != null)
                {
                    particle.renderer.sprite = ambientParticleSprite != null ? ambientParticleSprite : GetGlowSprite();
                    particle.renderer.gameObject.SetActive(false);
                }

                ambientParticles[i] = particle;
            }
        }

        private void SetAmbientParticlesActive(float baseRadius)
        {
            // 풀 재사용 또는 이전 연출 종료로 루트가 꺼져 있어도,
            // 파티클을 재생하는 시점에는 반드시 다시 표시한다.
            if (effectRoot != null && !effectRoot.gameObject.activeSelf)
            {
                effectRoot.gameObject.SetActive(true);
            }

            for (int i = 0; i < ambientParticles.Count; i++)
            {
                AmbientParticle particle = ambientParticles[i];
                if (particle.renderer != null)
                {
                    if (!particle.isActive)
                    {
                        particle.renderer.gameObject.SetActive(false);
                        ambientParticles[i] = particle;
                        continue;
                    }

                    float radius = baseRadius * particle.radiusMultiplier * Mathf.Lerp(
                        ambientParticleStartDistance,
                        ambientParticleEndDistance,
                        particle.phase);
                    Vector2 direction = Quaternion.Euler(0f, 0f, particle.angle) * Vector2.right;
                    float size = baseRadius * particle.sizeMultiplier * 1.3f;

                    SetParticleRenderer(particle.renderer, direction * radius, Vector2.one * size,
                        particle.rotationSpeed, WithAlpha(effectColor, 1f));
                    particle.renderer.gameObject.SetActive(true);
                    particle.isPlayingOneShot = PlayParticleAnimation(particle);

                    if (!particle.isPlayingOneShot)
                    {
                        particle.isActive = false;
                        particle.renderer.gameObject.SetActive(false);
                    }
                }

                ambientParticles[i] = particle;
            }
        }

        private void UpdateAmbientParticles()
        {
            bool hasActiveParticle = false;

            for (int i = 0; i < ambientParticles.Count; i++)
            {
                AmbientParticle particle = ambientParticles[i];
                if (!particle.isActive || particle.renderer == null) continue;

                if (particle.isPlayingOneShot && Time.time >= particle.animationEndTime)
                {
                    particle.isActive = false;
                    particle.isPlayingOneShot = false;
                    particle.renderer.gameObject.SetActive(false);
                    ambientParticles[i] = particle;
                    continue;
                }

                hasActiveParticle = true;
            }

            if (!hasActiveParticle)
            {
                FinishAmbientParticles();
            }
        }

        private void FinishAmbientParticles()
        {
            // 파티클이 모두 끝났다고 효과 루트까지 끄면, 풀 재사용 시점과
            // Animator 갱신 순서에 따라 다음 연출이 보이지 않을 수 있다.
            // 루트는 부모 드롭 유물이 활성인 동안 유지하고, 각 파티클만 끈다.
            isPlaying = false;
            isRevealing = false;
            isAmbient = false;
            isLandingBurst = false;
            showAmbientParticles = false;

            if (coreRenderer != null)
            {
                coreRenderer.color = Color.clear;
            }

            if (haloRenderer != null)
            {
                haloRenderer.color = Color.clear;
            }

            for (int i = 0; i < ambientParticles.Count; i++)
            {
                AmbientParticle particle = ambientParticles[i];
                particle.isActive = false;
                particle.isPlayingOneShot = false;

                if (particle.renderer != null)
                {
                    particle.renderer.gameObject.SetActive(false);
                }

                ambientParticles[i] = particle;
            }
        }

        private void BeginAmbientParticleEmission()
        {
            if (effectRoot != null && !effectRoot.gameObject.activeSelf)
            {
                effectRoot.gameObject.SetActive(true);
            }

            int activeParticleIndex = 0;
            int activeParticleCount = GetActiveParticleCount();
            float animationDuration = GetBaseParticleTravelDuration();

            for (int i = 0; i < ambientParticles.Count; i++)
            {
                AmbientParticle particle = ambientParticles[i];
                particle.isPlayingOneShot = false;

                if (particle.renderer != null)
                {
                    particle.renderer.gameObject.SetActive(false);
                }

                if (particle.isActive)
                {
                    // 동시에 전부 터지지 않도록 첫 방출만 균등하게 분산한다.
                    particle.nextSpawnTime = Time.time + animationDuration * activeParticleIndex / activeParticleCount;
                    activeParticleIndex++;
                }

                ambientParticles[i] = particle;
            }
        }

        private void UpdateAmbientParticles(float baseRadius)
        {
            for (int i = 0; i < ambientParticles.Count; i++)
            {
                AmbientParticle particle = ambientParticles[i];
                if (!particle.isActive || particle.renderer == null)
                {
                    continue;
                }

                if (particle.isPlayingOneShot)
                {
                    if (Time.time >= particle.animationEndTime)
                    {
                        particle.isPlayingOneShot = false;
                        particle.renderer.gameObject.SetActive(false);
                        particle.nextSpawnTime = Time.time + ambientParticleRepeatDelay;
                    }
                    else
                    {
                        UpdateAmbientParticleMotion(particle, baseRadius);
                    }
                }

                if (!particle.isPlayingOneShot && Time.time >= particle.nextSpawnTime)
                {
                    StartAmbientParticle(ref particle, baseRadius);
                }

                ambientParticles[i] = particle;
            }
        }

        private void StartAmbientParticle(ref AmbientParticle particle, float baseRadius)
        {
            particle.angle = Random.Range(0f, 360f);
            particle.speed = Random.Range(0.8f, 1.15f) * ambientParticleSpeed;
            particle.radiusMultiplier = Random.Range(0.9f, 1.1f);
            particle.sizeMultiplier = Random.Range(0.2f, 0.32f) * intensity;
            particle.rotationSpeed = Random.Range(-140f, 140f);
            particle.startTime = Time.time;
            particle.travelDuration = GetBaseParticleTravelDuration() / Random.Range(0.8f, 1.15f);

            Vector2 direction = Quaternion.Euler(0f, 0f, particle.angle) * Vector2.right;
            float startRadius = baseRadius * ambientParticleStartDistance;
            float size = baseRadius * particle.sizeMultiplier * 1.3f * ambientParticleVisualScale;
            particle.renderer.sprite = ambientParticleSprite != null ? ambientParticleSprite : GetGlowSprite();
            if (particle.animator != null)
            {
                particle.animator.enabled = false;
            }

            SetParticleRenderer(particle.renderer, direction * startRadius, Vector2.one * size,
                particle.rotationSpeed, GetParticleColor(1f));
            particle.renderer.gameObject.SetActive(true);
            particle.isPlayingOneShot = true;
            particle.animationEndTime = Time.time + particle.travelDuration;
        }

        private void UpdateAmbientParticleMotion(AmbientParticle particle, float baseRadius)
        {
            float progress = Mathf.InverseLerp(particle.startTime, particle.animationEndTime, Time.time);
            float movementProgress = Mathf.SmoothStep(0f, 1f, progress);
            float radius = baseRadius * particle.radiusMultiplier * Mathf.Lerp(
                ambientParticleStartDistance,
                ambientParticleEndDistance,
                movementProgress);
            float size = baseRadius * particle.sizeMultiplier * 1.3f * ambientParticleVisualScale;
            float rotation = particle.rotationSpeed + particle.rotationSpeed * progress;
            Vector2 direction = Quaternion.Euler(0f, 0f, particle.angle) * Vector2.right;
            float alpha = 1f - movementProgress;

            SetParticleRenderer(particle.renderer, direction * radius, Vector2.one * size,
                rotation, GetParticleColor(alpha));
        }

        private int GetActiveParticleCount()
        {
            int count = 0;
            for (int i = 0; i < ambientParticles.Count; i++)
            {
                if (ambientParticles[i].isActive)
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
        }

        private void PrepareParticleAnimation(ref AmbientParticle particle)
        {
            if (ambientParticleController == null || particle.renderer == null)
            {
                return;
            }

            Animation legacyAnimation = particle.renderer.GetComponent<Animation>();
            if (legacyAnimation != null)
            {
                legacyAnimation.enabled = false;
            }

            particle.animator = particle.renderer.GetComponent<Animator>();
            if (particle.animator == null)
            {
                particle.animator = particle.renderer.gameObject.AddComponent<Animator>();
            }

            particle.animator.runtimeAnimatorController = ambientParticleController;
            particle.animator.applyRootMotion = false;
        }

        private bool PlayParticleAnimation(AmbientParticle particle)
        {
            if (particle.animator == null || ambientParticleController == null)
            {
                return false;
            }

            float playbackSpeed = Mathf.Max(0.01f, GetParticleAnimationDuration() / particle.travelDuration);
            particle.animator.speed = playbackSpeed;
            particle.animator.Rebind();
            particle.animator.Update(0f);
            particle.animationEndTime = Time.time + particle.travelDuration;
            return true;
        }

        private float GetBaseParticleTravelDuration()
        {
            return ambientParticleTravelDuration / Mathf.Max(0.01f, ambientParticleSpeed);
        }

        private float GetParticleAnimationDuration()
        {
            float controllerDuration = 0f;

            if (ambientParticleController != null)
            {
                AnimationClip[] clips = ambientParticleController.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    AnimationClip clip = clips[i];
                    if (clip != null)
                    {
                        controllerDuration = Mathf.Max(controllerDuration, clip.length);
                    }
                }
            }

            // Inspector 값은 컨트롤러에 클립을 찾을 수 없을 때의 안전 종료 시간이다.
            // 정상적인 Loop Off 클립은 실제 길이만큼 끝 프레임까지 재생한다.
            return controllerDuration > 0f ? controllerDuration : ambientParticleAnimationDuration;
        }

        private SpriteRenderer CreateRenderer(string objectName)
        {
            GameObject visualObject = new GameObject(objectName);
            visualObject.layer = gameObject.layer;
            visualObject.transform.SetParent(effectRoot, false);

            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetGlowSprite();
            renderer.sortingLayerID = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0;
            renderer.sortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder + 1 : 1;
            return renderer;
        }

        private float GetBaseRadius()
        {
            if (sourceRenderer == null || sourceRenderer.sprite == null) return 0.75f;

            Bounds bounds = sourceRenderer.sprite.bounds;
            return Mathf.Max(0.55f, Mathf.Max(bounds.size.x, bounds.size.y) * 0.62f);
        }

        private static void SetRenderer(SpriteRenderer renderer, Vector2 size, Color color)
        {
            if (renderer == null) return;

            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            renderer.color = color;
        }

        private static void SetParticleRenderer(SpriteRenderer renderer, Vector2 position, Vector2 size, float rotation, Color color)
        {
            if (renderer == null) return;

            renderer.transform.localPosition = position;
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            renderer.color = color;
        }

        private static Sprite GetGlowSprite()
        {
            if (glowSprite != null) return glowSprite;

            const int textureSize = 16;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "RuntimeWorldRarityGlow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[textureSize * textureSize];
            float halfSize = (textureSize - 1) * 0.5f;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(halfSize, halfSize)) / halfSize;
                    float alpha = GetPixelAlpha(distance);
                    pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
            glowSprite.name = "RuntimeWorldRarityGlow";
            glowSprite.hideFlags = HideFlags.DontSave;
            return glowSprite;
        }

        private static float GetPixelAlpha(float distance)
        {
            if (distance <= 0.58f) return 1f;
            if (distance <= 0.73f) return 0.85f;
            if (distance <= 0.88f) return 0.55f;
            if (distance <= 1f) return 0.25f;
            return 0f;
        }

        private static Color GetColor(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Legendary => new Color(1f, 0.42f, 0.02f),
                Rarity.Epic => new Color(0.58f, 0.1f, 1f),
                Rarity.Rare => new Color(0.02f, 0.46f, 1f),
                Rarity.Uncommon => new Color(0.2f, 0.9f, 0.72f),
                _ => new Color(1f, 0.78f, 0.16f)
            };
        }

        private static float GetIntensity(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Legendary => 1.25f,
                Rarity.Epic => 1.05f,
                Rarity.Rare => 0.85f,
                Rarity.Uncommon => 0.72f,
                _ => 0.62f
            };
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private Color GetParticleColor(float alpha)
        {
            // 원본은 작은 흰색 픽셀이므로 등급색을 조금 흰색 쪽으로 보정해
            // 배경 위에서도 중심부가 또렷하게 보이게 한다.
            return WithAlpha(Color.Lerp(effectColor, Color.white, 0.35f), alpha);
        }

        private struct AmbientParticle
        {
            public SpriteRenderer renderer;
            public Animator animator;
            public bool isActive;
            public bool isPlayingOneShot;
            public float angle;
            public float phase;
            public float speed;
            public float radiusMultiplier;
            public float sizeMultiplier;
            public float rotationSpeed;
            public float startTime;
            public float travelDuration;
            public float animationEndTime;
            public float nextSpawnTime;
        }
    }
}
