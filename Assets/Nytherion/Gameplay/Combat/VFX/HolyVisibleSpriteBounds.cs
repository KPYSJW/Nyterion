using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nytherion.GamePlay.Combat
{
    // 신성 이펙트에 사용할 실제 불투명 픽셀 경계를 스프라이트별로 저장합니다.
    internal static class HolyVisibleSpriteBounds
    {
        private sealed class CachedBounds
        {
            public Bounds bounds;
        }

        private static readonly Dictionary<Sprite, CachedBounds> Cache = new Dictionary<Sprite, CachedBounds>();

        public static Bounds GetLocalBounds(Sprite sprite)
        {
            if (sprite == null) return default;
            if (!Cache.TryGetValue(sprite, out CachedBounds cached))
            {
                cached = new CachedBounds { bounds = GetFallbackBounds(sprite) };
                Cache.Add(sprite, cached);
                RequestPixels(sprite, cached);
            }
            return cached.bounds;
        }

        private static Bounds GetFallbackBounds(Sprite sprite)
        {
            Bounds result = default;
            bool hasPoint = false;
            List<Vector2> points = new List<Vector2>();
            for (int shape = 0; shape < sprite.GetPhysicsShapeCount(); shape++)
            {
                sprite.GetPhysicsShape(shape, points);
                foreach (Vector2 point in points) Include(point, ref result, ref hasPoint);
            }
            if (!hasPoint)
            {
                foreach (Vector2 point in sprite.vertices) Include(point, ref result, ref hasPoint);
            }
            return hasPoint ? result : sprite.bounds;
        }

        private static void Include(Vector2 point, ref Bounds bounds, ref bool hasPoint)
        {
            if (hasPoint) bounds.Encapsulate(point);
            else
            {
                bounds = new Bounds(point, Vector3.zero);
                hasPoint = true;
            }
        }

        private static void RequestPixels(Sprite sprite, CachedBounds cached)
        {
            if (!SystemInfo.supportsAsyncGPUReadback ||
                (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)) return;

            try
            {
                // 읽기 가능 설정을 요구하지 않으며, 압축 및 아틀라스 오프셋을 고려합니다.
                Rect rect = sprite.textureRect;
                Vector2 offset = sprite.textureRectOffset;
                Vector2 pivot = sprite.pivot;
                float pixelsPerUnit = sprite.pixelsPerUnit;
                int width = Mathf.RoundToInt(rect.width);
                int height = Mathf.RoundToInt(rect.height);
                if (width <= 0 || height <= 0 || pixelsPerUnit <= 0f) return;
                AsyncGPUReadback.Request(sprite.texture, 0, Mathf.RoundToInt(rect.x), width,
                    Mathf.RoundToInt(rect.y), height, 0, 1, TextureFormat.RGBA32, request =>
                    {
                        if (request.hasError) return;
                        var pixels = request.GetData<Color32>();
                        if (pixels.Length < width * height) return;
                        int minX = width, minY = height, maxX = -1, maxY = -1;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                if (pixels[y * width + x].a == 0) continue;
                                minX = Mathf.Min(minX, x);
                                minY = Mathf.Min(minY, y);
                                maxX = Mathf.Max(maxX, x);
                                maxY = Mathf.Max(maxY, y);
                            }
                        }
                        if (maxX < minX || maxY < minY) return;
                        float left = (offset.x + minX - pivot.x) / pixelsPerUnit;
                        float bottom = (offset.y + minY - pivot.y) / pixelsPerUnit;
                        float right = (offset.x + maxX + 1 - pivot.x) / pixelsPerUnit;
                        float top = (offset.y + maxY + 1 - pivot.y) / pixelsPerUnit;
                        cached.bounds = new Bounds(new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f),
                            new Vector3(right - left, top - bottom));
                    });
            }
            catch (UnityException)
            {
                // 촘촘히 패킹된 아틀라스 등은 물리/메시 경계를 사용합니다.
            }
            catch (System.ArgumentException)
            {
                // 플랫폼에서 해당 텍스처 읽기를 지원하지 않으면 대체 경계를 유지합니다.
            }
        }
    }
}
