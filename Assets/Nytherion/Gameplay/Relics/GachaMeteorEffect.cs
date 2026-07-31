using UnityEngine;

namespace Nytherion.GamePlay.Relics
{
    /// <summary>
    /// 가챠 상자에서 유물이 날아갈 때 사용하는 런타임 유성 이펙트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GachaMeteorEffect : MonoBehaviour
    {
        private const int TailSegmentCount = 8;

        private static Sprite glowSprite;

        private Transform effectRoot;
        private SpriteRenderer sourceRenderer;
        private Sprite configuredHeadSprite;
        private SpriteRenderer headRenderer;
        private SpriteRenderer[] tailRenderers;
        private bool isPlaying;
        private Vector3 previousPosition;
        private Vector2 travelDirection = Vector2.up;
        private Color effectColor;
        private float headScale = 1.4f;
        private float tailLength = 1.05f;

        public void Configure(float newHeadScale, float newTailLength)
        {
            headScale = Mathf.Max(0.1f, newHeadScale);
            tailLength = Mathf.Max(0.05f, newTailLength);
        }

        public void Begin(SpriteRenderer relicRenderer, Color color, Sprite headSprite)
        {
            sourceRenderer = relicRenderer;
            effectColor = color;
            configuredHeadSprite = headSprite;
            EnsureVisuals();

            if (effectRoot == null)
            {
                return;
            }

            isPlaying = true;
            previousPosition = transform.position;
            effectRoot.gameObject.SetActive(true);
            UpdateVisuals();
        }

        public void End()
        {
            isPlaying = false;

            if (effectRoot != null)
            {
                effectRoot.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            End();
        }

        private void Update()
        {
            if (!isPlaying || effectRoot == null)
            {
                return;
            }

            Vector2 movement = (Vector2)(transform.position - previousPosition);
            if (movement.sqrMagnitude > 0.0001f)
            {
                travelDirection = movement.normalized;
            }

            previousPosition = transform.position;
            UpdateVisuals();
        }

        private void EnsureVisuals()
        {
            if (effectRoot != null)
            {
                if (headRenderer != null)
                {
                    headRenderer.sprite = configuredHeadSprite != null ? configuredHeadSprite : GetGlowSprite();
                }

                return;
            }

            GameObject rootObject = new GameObject("GachaMeteorEffect");
            rootObject.layer = gameObject.layer;
            effectRoot = rootObject.transform;
            effectRoot.SetParent(transform, false);

            headRenderer = CreateRenderer("MeteorHead", 3, configuredHeadSprite != null ? configuredHeadSprite : GetGlowSprite());
            tailRenderers = new SpriteRenderer[TailSegmentCount];

            for (int i = 0; i < TailSegmentCount; i++)
            {
                tailRenderers[i] = CreateRenderer($"MeteorTail_{i + 1}", 2, GetGlowSprite());
            }

            effectRoot.gameObject.SetActive(false);
        }

        private SpriteRenderer CreateRenderer(string objectName, int sortingOffset, Sprite sprite)
        {
            GameObject visualObject = new GameObject(objectName);
            visualObject.layer = gameObject.layer;
            visualObject.transform.SetParent(effectRoot, false);

            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0;
            renderer.sortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder + sortingOffset : sortingOffset;
            return renderer;
        }

        private void UpdateVisuals()
        {
            if (headRenderer == null || tailRenderers == null)
            {
                return;
            }

            float baseRadius = GetBaseRadius();
            float directionAngle = Mathf.Atan2(travelDirection.y, travelDirection.x) * Mathf.Rad2Deg;

            headRenderer.transform.localPosition = travelDirection * baseRadius * 0.15f;
            headRenderer.transform.localRotation = Quaternion.identity;
            headRenderer.transform.localScale = Vector3.one * baseRadius * headScale;
            headRenderer.color = WithAlpha(effectColor, 1f);

            for (int i = 0; i < tailRenderers.Length; i++)
            {
                SpriteRenderer tailRenderer = tailRenderers[i];
                if (tailRenderer == null)
                {
                    continue;
                }

                float normalizedIndex = (i + 0.5f) / tailRenderers.Length;
                float strength = 1f - normalizedIndex;
                float distance = baseRadius * tailLength * normalizedIndex;
                float length = baseRadius * (0.55f + strength * 0.7f);
                float width = baseRadius * (0.1f + strength * 0.2f);

                tailRenderer.transform.localPosition = -travelDirection * distance;
                tailRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, directionAngle);
                tailRenderer.transform.localScale = new Vector3(length, width, 1f);
                tailRenderer.color = WithAlpha(effectColor, 1f);
            }
        }

        private float GetBaseRadius()
        {
            if (sourceRenderer == null || sourceRenderer.sprite == null)
            {
                return 0.55f;
            }

            Vector2 spriteSize = sourceRenderer.sprite.bounds.size;
            return Mathf.Max(0.4f, Mathf.Max(spriteSize.x, spriteSize.y) * 0.55f);
        }

        private static Sprite GetGlowSprite()
        {
            if (glowSprite != null)
            {
                return glowSprite;
            }

            const int textureSize = 16;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "RuntimeGachaMeteorGlow",
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
            glowSprite.name = "RuntimeGachaMeteorGlow";
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

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
