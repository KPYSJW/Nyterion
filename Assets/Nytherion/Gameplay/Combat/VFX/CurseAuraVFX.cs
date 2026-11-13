using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>저주받은 적의 발 아래에 크기가 다른 저주 이펙트를 표시합니다.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class CurseAuraVFX : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;
        [SerializeField, InspectorName("몸체 높이 대비 이펙트 크기")]
        private Vector2 heightRange = new Vector2(0.7f, 1.2f);

        private static readonly float[] HorizontalOffsets = { -0.32f, 0f, 0.32f };
        private static readonly float[] SizeFactors = { 0.25f, 1f, 0.6f };
        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private readonly SpriteRenderer[] renderers = new SpriteRenderer[3];
        private SpriteRenderer body;
        private float elapsed;

        public void Play(SpriteRenderer source)
        {
            if (source == null || source.sprite == null || frames == null || frames.Length == 0) return;
            if (body != null) return;
            body = source;
            elapsed = 0f;
            EnsureRenderers();
            gameObject.SetActive(true);
            enabled = true;
            UpdateVisual();
        }

        private void EnsureRenderers()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) continue;
                GameObject child = new GameObject("저주 이펙트 " + (i + 1));
                child.transform.SetParent(transform, false);
                renderers[i] = child.AddComponent<SpriteRenderer>();
            }
        }

        private void LateUpdate()
        {
            if (body == null || !body.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            elapsed += Time.deltaTime;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (body.sprite == null || !body.enabled)
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
                return;
            }

            Bounds bounds = bodyBounds.GetBounds(body);
            float bottom = CurseSpritePixels.GetWorldBottom(body, bounds.min.y);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer effect = renderers[i];
                int frameIndex = (Mathf.FloorToInt(elapsed * Mathf.Max(1f, framesPerSecond)) + i * 3) % frames.Length;
                Sprite frame = frames[frameIndex];
                if (frame == null)
                {
                    effect.enabled = false;
                    continue;
                }

                float height = bounds.size.y * Mathf.Max(0f, Mathf.Lerp(heightRange.x, heightRange.y, SizeFactors[i]));
                float scale = height / Mathf.Max(0.001f, frame.bounds.size.y);
                Transform effectTransform = effect.transform;
                effectTransform.position = new Vector3(bounds.center.x + bounds.size.x * HorizontalOffsets[i],
                    bottom, body.transform.position.z);
                effectTransform.rotation = Quaternion.identity;
                Vector3 parentScale = effectTransform.parent.lossyScale;
                effectTransform.localScale = new Vector3(scale / SafeScale(parentScale.x), scale / SafeScale(parentScale.y), 1f);
                effect.sprite = frame;
                effect.sortingLayerID = body.sortingLayerID;
                effect.sortingOrder = body.sortingOrder + 1;
                effect.enabled = true;
            }
        }

        private static float SafeScale(float value) => Mathf.Abs(value) < 0.001f ? 0.001f : value;

        public void Stop()
        {
            body = null;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = false;
            enabled = false;
        }

        private void OnDisable() => Stop();
    }

    // 텍스처를 읽기 가능으로 바꾸지 않고 실제 불투명 픽셀의 높이를 스프라이트별로 저장합니다.
    internal static class CurseSpritePixels
    {
        private sealed class PixelBounds
        {
            public bool ready;
            public bool pending;
            public float bottom;
            public float top;
        }

        private static readonly Dictionary<Sprite, PixelBounds> Cache = new Dictionary<Sprite, PixelBounds>();

        public static float GetWorldBottom(SpriteRenderer renderer, float fallback)
        {
            return GetWorldBottom(renderer, renderer.sprite, fallback, out _);
        }

        public static float GetWorldBottom(SpriteRenderer renderer, Sprite sprite, float fallback, out bool pending)
        {
            pending = false;
            if (renderer == null || sprite == null) return fallback;
            if (!Cache.TryGetValue(sprite, out PixelBounds pixels))
            {
                pixels = new PixelBounds();
                Cache.Add(sprite, pixels);
                RequestPixels(sprite, pixels);
            }
            pending = pixels.pending;
            if (!pixels.ready) return fallback;
            float localBottom = renderer.flipY ? -pixels.top : pixels.bottom;
            float localTop = renderer.flipY ? -pixels.bottom : pixels.top;
            float y = renderer.transform.localToWorldMatrix.m11 < 0f ? localTop : localBottom;
            return renderer.transform.TransformPoint(0f, y, 0f).y;
        }

        private static void RequestPixels(Sprite sprite, PixelBounds pixels)
        {
            if (!SystemInfo.supportsAsyncGPUReadback ||
                (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)) return;

            try
            {
                Rect rect = sprite.textureRect;
                Vector2 offset = sprite.textureRectOffset;
                int width = Mathf.RoundToInt(rect.width);
                int height = Mathf.RoundToInt(rect.height);
                if (width <= 0 || height <= 0) return;
                float pivotY = sprite.pivot.y;
                float pixelsPerUnit = sprite.pixelsPerUnit;
                pixels.pending = true;
                AsyncGPUReadback.Request(sprite.texture, 0, Mathf.RoundToInt(rect.x), width,
                    Mathf.RoundToInt(rect.y), height, 0, 1, TextureFormat.RGBA32, request =>
                    {
                        pixels.pending = false;
                        if (request.hasError) return;
                        var data = request.GetData<Color32>();
                        if (data.Length < width * height) return;
                        int bottom = height;
                        int top = -1;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                if (data[y * width + x].a == 0) continue;
                                bottom = Mathf.Min(bottom, y);
                                top = y;
                                break;
                            }
                        }
                        if (top < 0) return;
                        pixels.bottom = (offset.y + bottom - pivotY) / pixelsPerUnit;
                        pixels.top = (offset.y + top + 1f - pivotY) / pixelsPerUnit;
                        pixels.ready = true;
                    });
            }
            catch (UnityException)
            {
                pixels.pending = false;
                // 픽셀 영역을 읽을 수 없는 스프라이트는 기존 몸체 경계를 사용합니다.
            }
        }
    }
}
