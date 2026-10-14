using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.Components
{
    /// <summary>아이콘을 원본 방향으로 표시하고 화면의 정수 픽셀 배율과 위치에 맞춘다.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public class PixelPerfectItemIcon : MonoBehaviour
    {
        private Image image;
        private RectTransform slot;
        private float iconScale = 1f;
        private readonly Vector3[] corners = new Vector3[4];
        private const float Padding = 8f;
        private const float DisplayScale = 2f;

        public static void Apply(Image image, RectTransform slot, float iconScale = 1f)
        {
            if (image == null || slot == null || image.transform == slot) return;
            PixelPerfectItemIcon layout = image.GetComponent<PixelPerfectItemIcon>();
            if (layout == null) layout = image.gameObject.AddComponent<PixelPerfectItemIcon>();
            layout.enabled = true;
            layout.image = image;
            layout.slot = slot;
            layout.iconScale = Mathf.Max(0.1f, iconScale);
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
            // 정렬된 기본 배율을 두 배로 키워 투명 여백이 있는 장비도 크게 표시한다.
            float pixelScale = fit >= 1f ? Mathf.Floor(fit + 0.0001f) : 1f / Mathf.Ceil(1f / fit);
            pixelScale *= DisplayScale;
            // 개별 무기의 크기 보정도 확대된 원본 픽셀의 정수 배율을 유지한다.
            pixelScale *= iconScale;
            if (pixelScale >= 1f) pixelScale = Mathf.Max(1f, Mathf.Floor(pixelScale + 0.0001f));
            Vector2 displaySize = sourceSize * pixelScale;

            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, slot.TransformPoint(slot.rect.center));
            Vector2 origin = center - displaySize * 0.5f;
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
    }
}
