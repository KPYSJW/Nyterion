using System.Collections.Generic;
using Nytherion.Core.Enums;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.Components
{
    /// <summary>
    /// 스프라이트 시트 없이 UI 슬롯에 빛 번짐, 방사광, 빛 조각을 표시한다.
    /// 생성되는 이미지는 런타임 전용이며, 기본 UI 머티리얼로 동작한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RarityLightEffect : MonoBehaviour
    {
        private const int RayCount = 8;
        private const int SparkCount = 12;
        private const float BurstDuration = 0.72f;

        private static Sprite glowSprite;

        private RectTransform hostRectTransform;
        private RectTransform effectRoot;
        private Image flashImage;
        private Image haloImage;
        private readonly List<RayVisual> rayVisuals = new List<RayVisual>();
        private readonly List<SparkVisual> sparkVisuals = new List<SparkVisual>();

        private bool isPlaying;
        private bool isStrongReveal;
        private float elapsedTime;
        private float intensity;
        private float ambientAlpha;
        private Color effectColor;

        public static RarityLightEffect GetOrAdd(GameObject target)
        {
            if (target == null) return null;

            RarityLightEffect effect = target.GetComponent<RarityLightEffect>();
            return effect != null ? effect : target.AddComponent<RarityLightEffect>();
        }

        /// <summary>
        /// Rare 이상일 때만 효과를 재생한다. strongReveal은 뽑기 결과처럼 강한 첫 등장 연출에 사용한다.
        /// </summary>
        public void Play(Rarity rarity, RectTransform foreground, bool strongReveal)
        {
            if (rarity < Rarity.Rare)
            {
                Clear();
                return;
            }

            EnsureVisuals();
            if (effectRoot == null) return;

            isPlaying = true;
            isStrongReveal = strongReveal;
            elapsedTime = 0f;
            effectColor = GetColor(rarity);
            intensity = GetIntensity(rarity);
            ambientAlpha = strongReveal ? 0.38f * intensity : 0.24f * intensity;

            PlaceBehindForeground(foreground);
            ConfigureVisuals(rarity);
            effectRoot.gameObject.SetActive(true);
        }

        public void Clear()
        {
            isPlaying = false;
            elapsedTime = 0f;

            if (effectRoot != null)
            {
                effectRoot.gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            hostRectTransform = transform as RectTransform;
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Update()
        {
            if (!isPlaying || effectRoot == null || !effectRoot.gameObject.activeSelf) return;

            elapsedTime += Time.unscaledDeltaTime;

            Vector2 baseSize = GetBaseSize();
            UpdateBurst(baseSize);
            UpdateRays(baseSize);
            UpdateSparks(baseSize);
        }

        private void EnsureVisuals()
        {
            if (effectRoot != null) return;

            if (hostRectTransform == null)
            {
                hostRectTransform = transform as RectTransform;
            }

            if (hostRectTransform == null) return;

            GameObject rootObject = new GameObject("RarityLightEffect", typeof(RectTransform));
            rootObject.layer = gameObject.layer;

            effectRoot = rootObject.GetComponent<RectTransform>();
            effectRoot.SetParent(hostRectTransform, false);
            effectRoot.anchorMin = Vector2.zero;
            effectRoot.anchorMax = Vector2.one;
            effectRoot.offsetMin = Vector2.zero;
            effectRoot.offsetMax = Vector2.zero;
            effectRoot.pivot = new Vector2(0.5f, 0.5f);

            haloImage = CreateLightImage("Halo", effectRoot);
            flashImage = CreateLightImage("Flash", effectRoot);

            for (int i = 0; i < RayCount; i++)
            {
                Image rayImage = CreateLightImage($"Ray_{i + 1}", effectRoot);
                rayVisuals.Add(new RayVisual { image = rayImage });
            }

            for (int i = 0; i < SparkCount; i++)
            {
                Image sparkImage = CreateLightImage($"Spark_{i + 1}", effectRoot);
                sparkVisuals.Add(new SparkVisual { image = sparkImage });
            }

            effectRoot.gameObject.SetActive(false);
        }

        private void ConfigureVisuals(Rarity rarity)
        {
            int activeRayCount = rarity == Rarity.Legendary ? RayCount : rarity == Rarity.Epic ? 6 : 4;
            int activeSparkCount = rarity == Rarity.Legendary ? SparkCount : rarity == Rarity.Epic ? 9 : 6;

            for (int i = 0; i < rayVisuals.Count; i++)
            {
                RayVisual visual = rayVisuals[i];
                visual.isActive = i < activeRayCount;
                visual.angle = Random.Range(0f, 360f);
                visual.delay = Random.Range(0f, 0.13f);
                visual.lengthMultiplier = Random.Range(0.95f, 1.45f) * intensity;
                visual.widthMultiplier = Random.Range(0.08f, 0.15f) * intensity;

                if (visual.image != null)
                {
                    visual.image.gameObject.SetActive(visual.isActive);
                }

                rayVisuals[i] = visual;
            }

            for (int i = 0; i < sparkVisuals.Count; i++)
            {
                SparkVisual visual = sparkVisuals[i];
                visual.isActive = i < activeSparkCount;
                visual.angle = Random.Range(0f, 360f);
                visual.startDelay = Random.Range(isStrongReveal ? 0.18f : 0.05f, isStrongReveal ? 0.65f : 0.35f);
                visual.speed = Random.Range(0.34f, 0.62f);
                visual.radiusMultiplier = Random.Range(0.48f, 1.1f);
                visual.sizeMultiplier = Random.Range(0.055f, 0.1f) * intensity;
                visual.rotationSpeed = Random.Range(-100f, 100f);

                if (visual.image != null)
                {
                    visual.image.gameObject.SetActive(visual.isActive);
                }

                sparkVisuals[i] = visual;
            }
        }

        private void PlaceBehindForeground(RectTransform foreground)
        {
            if (effectRoot == null) return;

            if (foreground != null && foreground.parent == effectRoot.parent)
            {
                effectRoot.SetSiblingIndex(foreground.GetSiblingIndex());
            }
            else
            {
                effectRoot.SetAsFirstSibling();
            }
        }

        private void UpdateBurst(Vector2 baseSize)
        {
            float burstProgress = Mathf.Clamp01(elapsedTime / BurstDuration);
            float burstFade = 1f - burstProgress;
            float flashStrength = isStrongReveal ? 0.82f : 0.48f;

            SetImage(flashImage, baseSize * Mathf.Lerp(0.45f, 2.2f * intensity, burstProgress),
                WithAlpha(Color.Lerp(effectColor, Color.white, 0.72f), burstFade * flashStrength));

            float haloPulse = 0.78f + Mathf.Sin(elapsedTime * 2.6f) * 0.12f;
            SetImage(haloImage, baseSize * (1.55f + haloPulse * 0.7f * intensity),
                WithAlpha(effectColor, ambientAlpha * haloPulse));
        }

        private void UpdateRays(Vector2 baseSize)
        {
            float rayDuration = isStrongReveal ? 0.65f : 0.45f;

            for (int i = 0; i < rayVisuals.Count; i++)
            {
                RayVisual visual = rayVisuals[i];
                if (!visual.isActive || visual.image == null) continue;

                float progress = Mathf.Clamp01((elapsedTime - visual.delay) / rayDuration);
                float alpha = elapsedTime < visual.delay ? 0f : (1f - progress) * (isStrongReveal ? 0.42f : 0.24f);
                float length = baseSize.magnitude * visual.lengthMultiplier;
                float width = Mathf.Max(8f, baseSize.magnitude * visual.widthMultiplier);
                Vector2 direction = Quaternion.Euler(0f, 0f, visual.angle) * Vector2.right;

                RectTransform rayRect = visual.image.rectTransform;
                rayRect.anchoredPosition = direction * length * Mathf.Lerp(0.06f, 0.3f, progress);
                rayRect.sizeDelta = new Vector2(length, width);
                rayRect.localEulerAngles = new Vector3(0f, 0f, visual.angle);
                visual.image.color = WithAlpha(Color.Lerp(effectColor, Color.white, 0.55f), alpha);
            }
        }

        private void UpdateSparks(Vector2 baseSize)
        {
            for (int i = 0; i < sparkVisuals.Count; i++)
            {
                SparkVisual visual = sparkVisuals[i];
                if (!visual.isActive || visual.image == null) continue;

                float timeSinceStart = elapsedTime - visual.startDelay;
                if (timeSinceStart < 0f)
                {
                    visual.image.color = Color.clear;
                    continue;
                }

                float cycle = Mathf.Repeat(timeSinceStart * visual.speed, 1f);
                float rise = Mathf.SmoothStep(0f, 1f, cycle);
                float alpha = Mathf.Sin(cycle * Mathf.PI);
                Vector2 direction = Quaternion.Euler(0f, 0f, visual.angle + Mathf.Sin(elapsedTime * 1.4f + i) * 10f) * Vector2.right;
                float radius = baseSize.magnitude * visual.radiusMultiplier * Mathf.Lerp(0.15f, 0.72f, rise);
                float size = Mathf.Max(5f, baseSize.magnitude * visual.sizeMultiplier * (0.7f + alpha * 0.55f));

                RectTransform sparkRect = visual.image.rectTransform;
                sparkRect.anchoredPosition = direction * radius;
                sparkRect.sizeDelta = new Vector2(size, size);
                sparkRect.localEulerAngles = new Vector3(0f, 0f, elapsedTime * visual.rotationSpeed);
                visual.image.color = WithAlpha(Color.Lerp(effectColor, Color.white, 0.65f), alpha * ambientAlpha * 1.6f);
            }
        }

        private Vector2 GetBaseSize()
        {
            if (hostRectTransform == null) return new Vector2(100f, 100f);

            Vector2 size = hostRectTransform.rect.size;
            return new Vector2(Mathf.Max(size.x, 64f), Mathf.Max(size.y, 64f));
        }

        private static Image CreateLightImage(string objectName, Transform parent)
        {
            GameObject lightObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lightObject.layer = parent.gameObject.layer;

            RectTransform rectTransform = lightObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            Image image = lightObject.GetComponent<Image>();
            image.sprite = GetGlowSprite();
            image.raycastTarget = false;
            image.maskable = false;
            return image;
        }

        private static Sprite GetGlowSprite()
        {
            if (glowSprite != null) return glowSprite;

            const int textureSize = 128;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "RuntimeRarityGlow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[textureSize * textureSize];
            float halfSize = (textureSize - 1) * 0.5f;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(halfSize, halfSize)) / halfSize;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
            glowSprite.name = "RuntimeRarityGlow";
            glowSprite.hideFlags = HideFlags.DontSave;
            return glowSprite;
        }

        private static void SetImage(Image image, Vector2 size, Color color)
        {
            if (image == null) return;

            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.sizeDelta = size;
            image.color = color;
        }

        private static Color GetColor(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Legendary => new Color(1f, 0.67f, 0.14f),
                Rarity.Epic => new Color(0.78f, 0.3f, 1f),
                _ => new Color(0.23f, 0.72f, 1f)
            };
        }

        private static float GetIntensity(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Legendary => 1.25f,
                Rarity.Epic => 1.05f,
                _ => 0.85f
            };
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private struct RayVisual
        {
            public Image image;
            public bool isActive;
            public float angle;
            public float delay;
            public float lengthMultiplier;
            public float widthMultiplier;
        }

        private struct SparkVisual
        {
            public Image image;
            public bool isActive;
            public float angle;
            public float startDelay;
            public float speed;
            public float radiusMultiplier;
            public float sizeMultiplier;
            public float rotationSpeed;
        }
    }
}
