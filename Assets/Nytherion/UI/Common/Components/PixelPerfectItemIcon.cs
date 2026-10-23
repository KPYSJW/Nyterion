using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Nytherion.UI.Components
{
    /// <summary>아이콘을 원본 방향으로 표시하고 실제 그림 크기와 화면 픽셀 위치에 맞춘다.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public class PixelPerfectItemIcon : MonoBehaviour
    {
        private Image image;
        private RectTransform slot;
        private float iconScale = 1f;
        private bool fitVisibleBounds;
        private float sizeMultiplier = 1f;
        private readonly Vector3[] corners = new Vector3[4];
        private const float Padding = 8f;
        private const float DisplayScale = 2f;
        private static readonly Dictionary<Sprite, Rect> VisibleRects = new Dictionary<Sprite, Rect>();

        public static void Apply(Image image, RectTransform slot, float iconScale = 1f, bool fitVisibleBounds = false,
            float sizeMultiplier = 1f)
        {
            if (image == null || slot == null || image.transform == slot) return;
            PixelPerfectItemIcon layout = image.GetComponent<PixelPerfectItemIcon>();
            if (layout == null) layout = image.gameObject.AddComponent<PixelPerfectItemIcon>();
            layout.enabled = true;
            layout.image = image;
            layout.slot = slot;
            layout.iconScale = Mathf.Max(0.1f, iconScale);
            layout.fitVisibleBounds = fitVisibleBounds;
            layout.sizeMultiplier = Mathf.Max(0.1f, sizeMultiplier);
            layout.Refresh();
        }

        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (image == null || !image.enabled || image.sprite == null || slot == null) return;
            Canvas canvas = image.canvas != null ? image.canvas.rootCanvas : null;
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && camera == null) return;

            slot.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
            Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);
            Vector2 slotSize = slot.rect.size;
            Vector2 screenSize = new Vector2(Vector2.Distance(bottomLeft, bottomRight),
                Vector2.Distance(bottomLeft, topLeft));
            if (slotSize.x <= 0f || slotSize.y <= 0f || screenSize.x <= 0f || screenSize.y <= 0f) return;

            Vector2 density = new Vector2(screenSize.x / slotSize.x, screenSize.y / slotSize.y);
            Vector2 available = screenSize - new Vector2(Padding * 2f * density.x, Padding * 2f * density.y);
            Vector2 sourceSize = image.sprite.rect.size;
            if (available.x <= 0f || available.y <= 0f || sourceSize.x <= 0f || sourceSize.y <= 0f) return;
            float fit = Mathf.Min(available.x / sourceSize.x, available.y / sourceSize.y);
            Rect visibleRect = GetVisibleRect(image.sprite);
            float visibleFit = Mathf.Min(available.x / visibleRect.width, available.y / visibleRect.height);
            // 정렬된 기본 배율을 두 배로 키워 투명 여백이 있는 장비도 크게 표시한다.
            float pixelScale = fit >= 1f ? Mathf.Floor(fit + 0.0001f) : 1f / Mathf.Ceil(1f / fit);
            pixelScale *= DisplayScale;
            // 투명 여백을 제외한 그림만 제한해 기존 확대 크기를 최대한 유지한다.
            pixelScale = Mathf.Min(pixelScale * iconScale, visibleFit);
            // 개별 확대를 요청한 장비는 정수 배율 내림으로 확대 효과가 사라지지 않게 한다.
            if (pixelScale >= 1f && iconScale <= 1f) pixelScale = Mathf.Floor(pixelScale + 0.0001f);
            // 무기는 PNG 여백이나 화면 배율의 정수 내림에 따라 작아지지 않게 실제 그림으로 맞춘다.
            // 명시적으로 축소한 장비와 비무기 아이템은 기존 픽셀 배율을 유지한다.
            if (fitVisibleBounds && iconScale >= 1f) pixelScale = visibleFit;
            // 기존 무기별 크기 보정 뒤에 적용해 0.9가 현재 표시 크기의 90%가 되게 한다.
            pixelScale = Mathf.Min(pixelScale * sizeMultiplier, visibleFit);
            Vector2 displaySize = sourceSize * pixelScale;

            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, slot.TransformPoint(slot.rect.center));
            // 파일의 중심 대신 실제 그림의 중심을 슬롯 가운데에 놓는다.
            Vector2 origin = center - visibleRect.center * pixelScale;
            // 홀수 너비도 좌우 경계가 정수 픽셀에 놓이도록 왼쪽 아래부터 정렬한다.
            Vector2 snappedCenter = new Vector2(Mathf.Round(origin.x), Mathf.Round(origin.y)) + displaySize * 0.5f;
            RectTransform iconRect = image.rectTransform;
            RectTransform parent = iconRect.parent as RectTransform;
            if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                snappedCenter, camera, out Vector2 position)) return;

            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            Vector2 middle = Vector2.one * 0.5f;
            if (iconRect.anchorMin != middle) iconRect.anchorMin = middle;
            if (iconRect.anchorMax != middle) iconRect.anchorMax = middle;
            if (iconRect.pivot != middle) iconRect.pivot = middle;
            if (iconRect.localRotation != Quaternion.identity) iconRect.localRotation = Quaternion.identity;
            if (iconRect.localScale != Vector3.one) iconRect.localScale = Vector3.one;
            Vector2 size = new Vector2(displaySize.x / density.x, displaySize.y / density.y);
            if (iconRect.sizeDelta != size) iconRect.sizeDelta = size;
            Vector3 localPosition = new Vector3(position.x, position.y, iconRect.localPosition.z);
            if (iconRect.localPosition != localPosition) iconRect.localPosition = localPosition;
        }

        private static Rect GetVisibleRect(Sprite sprite)
        {
            if (VisibleRects.TryGetValue(sprite, out Rect cached)) return cached;
            Rect visible = new Rect(Vector2.zero, sprite.rect.size);
            // 아틀라스에서 회전·잘림이 적용된 스프라이트는 원래 사각형을 사용한다.
            if (sprite.packed) return visible;

            Texture2D texture = sprite.texture;
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D readback = null;
            try
            {
                // 읽기 비허용인 원본 에셋도 변경 없이 확인한다. 스프라이트마다 최초 한 번만 읽는다.
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                int width = Mathf.RoundToInt(sprite.rect.width);
                int height = Mathf.RoundToInt(sprite.rect.height);
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(sprite.rect, 0, 0, false);
                Color32[] pixels = readback.GetPixels32();
                int minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a == 0) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
                if (maxX >= minX && maxY >= minY)
                    visible = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                VisibleRects[sprite] = visible;
                return visible;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (readback != null)
                {
                    if (Application.isPlaying) Destroy(readback);
                    else DestroyImmediate(readback);
                }
            }
        }
    }
}
