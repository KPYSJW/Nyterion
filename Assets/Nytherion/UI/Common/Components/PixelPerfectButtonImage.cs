using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.Components
{
    /// <summary>버튼의 논리 픽셀을 정수 화면 배율로 표시하고 왼쪽 아래 경계를 픽셀 격자에 맞춘다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public class PixelPerfectButtonImage : BaseMeshEffect
    {
        [SerializeField, Min(1)] private int sourcePixelBlockSize = 1;
        private readonly Vector3[] corners = new Vector3[4];
        private Rect previousScreenRect;

        public static void Apply(Image image, int sourcePixelBlockSize)
        {
            if (image == null) return;
            var effect = image.GetComponent<PixelPerfectButtonImage>();
            if (effect == null) effect = image.gameObject.AddComponent<PixelPerfectButtonImage>();
            effect.sourcePixelBlockSize = Mathf.Max(1, sourcePixelBlockSize);
            effect.enabled = true;
            image.SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.preWillRenderCanvases += Refresh;
        }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= Refresh;
            if (graphic is Image image) image.raycastPadding = Vector4.zero;
            base.OnDisable();
        }

        private void Refresh()
        {
            if (!TryGetScreenRect(out Rect screenRect, out _)) return;
            if (screenRect == previousScreenRect) return;
            previousScreenRect = screenRect;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount == 0) return;
            Image image = graphic as Image;
            if (image == null || image.type != Image.Type.Simple || image.useSpriteMesh) return;
            Sprite sprite = image.overrideSprite;
            if (sprite == null || !TryGetScreenRect(out Rect screenRect, out Camera camera)) return;
            Vector2 logicalSize = sprite.rect.size / Mathf.Max(1, sourcePixelBlockSize);
            if (logicalSize.x <= 0f || logicalSize.y <= 0f) return;

            // 3배 확대된 원본도 실제 픽셀 블록을 기준으로 같은 굵기로 그린다.
            int scale = Mathf.Max(1, Mathf.FloorToInt(
                Mathf.Min(screenRect.width / logicalSize.x, screenRect.height / logicalSize.y) + 0.5001f));
            Vector2 displaySize = logicalSize * scale;
            Vector2 min = new Vector2(Mathf.Round(screenRect.center.x - displaySize.x * 0.5f),
                Mathf.Round(screenRect.center.y - displaySize.y * 0.5f));
            RectTransform rect = image.rectTransform;
            Rect adjusted = image.GetPixelAdjustedRect();
            Vector2 originalMin = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(adjusted.min));
            Vector2 originalMax = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(adjusted.max));
            Vector2 originalSize = originalMax - originalMin;
            if (originalSize.x <= 0f || originalSize.y <= 0f) return;
            // 실제 그림이 정수 배율로 커지거나 작아진 만큼 클릭 경계도 함께 맞춘다.
            Vector2 unitsPerPixel = new Vector2(rect.rect.width / screenRect.width, rect.rect.height / screenRect.height);
            Vector2 max = min + displaySize;
            image.raycastPadding = new Vector4((min.x - screenRect.xMin) * unitsPerPixel.x,
                (min.y - screenRect.yMin) * unitsPerPixel.y,
                (screenRect.xMax - max.x) * unitsPerPixel.x,
                (screenRect.yMax - max.y) * unitsPerPixel.y);
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(vertex.position));
                Vector2 normalized = new Vector2((screen.x - originalMin.x) / originalSize.x,
                    (screen.y - originalMin.y) / originalSize.y);
                Vector2 aligned = min + Vector2.Scale(normalized, displaySize);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, aligned, camera, out Vector2 local)) return;
                vertex.position = new Vector3(local.x, local.y, vertex.position.z);
                vertices.SetUIVertex(vertex, i);
            }
        }

        private bool TryGetScreenRect(out Rect screenRect, out Camera camera)
        {
            screenRect = default;
            camera = null;
            if (graphic == null || graphic.canvas == null) return false;
            Canvas canvas = graphic.canvas.rootCanvas;
            if (canvas.renderMode == RenderMode.WorldSpace) return false;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                camera = canvas.worldCamera;
                if (camera == null) return false;
            }
            graphic.rectTransform.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            if (Mathf.Abs(topLeft.x - bottomLeft.x) > 0.01f || Mathf.Abs(topLeft.y - topRight.y) > 0.01f) return false;
            screenRect = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
            return screenRect.width > 0f && screenRect.height > 0f;
        }
    }
}
