using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.Skill
{
    /// <summary>
    /// 슬롯의 배치와 아이콘 크기를 유지하면서 배경 픽셀을 화면의 정수 배율로 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class PixelPerfectSlotBackground : BaseMeshEffect
    {
        private readonly Vector3[] corners = new Vector3[4];
        private Rect previousScreenRect;
        private bool hasPreviousScreenRect;

        protected override void OnEnable()
        {
            base.OnEnable();
            hasPreviousScreenRect = false;
            Canvas.preWillRenderCanvases += RefreshScreenRect;
        }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= RefreshScreenRect;
            base.OnDisable();
        }

        private void RefreshScreenRect()
        {
            if (!TryGetScreenRect(out Rect screenRect, out _)) return;
            // Canvas 배율만 변하는 경우에도 화면 크기에 맞춰 메시를 다시 만든다.
            if (!hasPreviousScreenRect || previousScreenRect != screenRect)
            {
                previousScreenRect = screenRect;
                hasPreviousScreenRect = true;
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount == 0) return;
            Image image = graphic as Image;
            if (image == null || image.type != Image.Type.Simple || image.useSpriteMesh || image.preserveAspect) return;
            Sprite sprite = image.overrideSprite;
            if (sprite == null || image.mainTexture.filterMode != FilterMode.Point) return;
            if (!TryGetScreenRect(out Rect screenRect, out Camera camera)) return;

            Vector2 spriteSize = sprite.rect.size;
            float requestedScale = Mathf.Min(screenRect.width / spriteSize.x, screenRect.height / spriteSize.y);
            // 원본보다 작은 슬롯은 기존 축소 표시를 유지한다.
            if (requestedScale < 1f) return;
            // 배경을 축소하면 기존 아이콘이 테두리를 덮으므로 가장 가까운 큰 정수 배율을 사용한다.
            int pixelScale = Mathf.CeilToInt(requestedScale - 0.0001f);

            Vector2 size = spriteSize * pixelScale;
            Vector2 min = new Vector2(
                Mathf.Round(screenRect.center.x - size.x * 0.5f),
                Mathf.Round(screenRect.center.y - size.y * 0.5f));

            UIVertex vertex = new UIVertex();
            RectTransform rectTransform = image.rectTransform;
            // Image가 먼저 적용한 Canvas Pixel Perfect 보정까지 반영해 원래 메시 좌표를 읽는다.
            Rect adjustedRect = image.GetPixelAdjustedRect();
            Vector2 sourceMin = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(adjustedRect.min));
            Vector2 sourceMax = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(adjustedRect.max));
            Vector2 sourceSize = sourceMax - sourceMin;
            if (sourceSize.x <= 0f || sourceSize.y <= 0f) return;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(vertex.position));
                Vector2 normalizedPosition = new Vector2(
                    (screenPosition.x - sourceMin.x) / sourceSize.x,
                    (screenPosition.y - sourceMin.y) / sourceSize.y);
                Vector2 alignedPosition = min + Vector2.Scale(normalizedPosition, size);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, alignedPosition, camera, out Vector2 localPosition)) return;
                vertex.position = new Vector3(localPosition.x, localPosition.y, vertex.position.z);
                vertices.SetUIVertex(vertex, i);
            }
        }

        private bool TryGetScreenRect(out Rect screenRect, out Camera camera)
        {
            screenRect = default;
            camera = null;
            if (graphic == null || graphic.canvas == null) return false;
            Canvas rootCanvas = graphic.canvas.rootCanvas;
            if (rootCanvas.renderMode == RenderMode.WorldSpace) return false;
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                camera = rootCanvas.worldCamera;
                if (camera == null) return false;
            }

            graphic.rectTransform.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            // 회전되거나 뒤집힌 UI에는 화면 축 기준 보정을 적용하지 않는다.
            if (Mathf.Abs(topLeft.x - bottomLeft.x) > 0.01f || Mathf.Abs(topLeft.y - topRight.y) > 0.01f) return false;
            screenRect = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
            return screenRect.width > 0f && screenRect.height > 0f;
        }
    }
}
