using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.Components
{
    /// <summary>슬롯 테두리를 화면의 정수 픽셀에 맞춰 배율에 따른 선 굵기 차이를 막는다.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public class PixelPerfectSlotFrame : BaseMeshEffect
    {
        private Image frame;
        private Matrix4x4 previousMatrix;
        private Vector2 previousSize;
        private Vector2 previousScreenSize;
        private Vector2 previousOrigin;

        public static void Apply(Image image)
        {
            if (image == null || image.sprite == null) return;
            image.type = Image.Type.Sliced;
            image.preserveAspect = true;
            PixelPerfectSlotFrame effect = image.GetComponent<PixelPerfectSlotFrame>();
            if (effect == null) effect = image.gameObject.AddComponent<PixelPerfectSlotFrame>();
            effect.Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Refresh();
        }

        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (frame == null) frame = GetComponent<Image>();
            Canvas imageCanvas = frame.canvas;
            Canvas canvas = imageCanvas != null ? imageCanvas.rootCanvas : null;
            Sprite sprite = frame.overrideSprite;
            if (canvas == null || sprite == null || canvas.renderMode == RenderMode.WorldSpace) return;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform rect = frame.rectTransform;
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.zero));
            float rectWidth = rect.rect.width;
            if (rectWidth <= 0f) return;
            // 1 UI 단위의 차 대신 전체 폭을 측정해 카메라 좌표의 부동소수점 오차를 줄인다.
            Vector2 left = RectTransformUtility.WorldToScreenPoint(camera,
                rect.TransformPoint(new Vector3(rect.rect.xMin, 0f, 0f)));
            Vector2 right = RectTransformUtility.WorldToScreenPoint(camera,
                rect.TransformPoint(new Vector3(rect.rect.xMax, 0f, 0f)));
            float density = Vector2.Distance(left, right) / rectWidth;
            if (density <= 0f) return;

            // 중앙만 늘리고 모서리와 외곽선의 원본 픽셀은 같은 정수 배율로 그린다.
            float width = rect.rect.width * density;
            float height = rect.rect.height * density;
            int pixelScale = Mathf.Max(1, Mathf.FloorToInt(
                Mathf.Min(width / sprite.rect.width, height / sprite.rect.height) + 0.5001f));
            Vector4 border = sprite.border;
            if (border.x + border.z > 0f && border.y + border.w > 0f)
                pixelScale = Mathf.Min(pixelScale, Mathf.Max(1, Mathf.FloorToInt(
                    Mathf.Min(width / (border.x + border.z), height / (border.y + border.w)))));
            float multiplier = density * imageCanvas.referencePixelsPerUnit / (sprite.pixelsPerUnit * pixelScale);
            if (!Mathf.Approximately(frame.pixelsPerUnitMultiplier, multiplier))
                frame.pixelsPerUnitMultiplier = multiplier;

            Matrix4x4 matrix = rect.localToWorldMatrix;
            Vector2 size = rect.rect.size;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (matrix == previousMatrix && size == previousSize && screenSize == previousScreenSize && origin == previousOrigin) return;
            previousMatrix = matrix;
            previousSize = size;
            previousScreenSize = screenSize;
            previousOrigin = origin;
            frame.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive()) return;
            if (frame == null) frame = GetComponent<Image>();
            Canvas canvas = frame.canvas != null ? frame.canvas.rootCanvas : null;
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform rect = frame.rectTransform;
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(vertex.position));
                screen.x = Mathf.Round(screen.x);
                screen.y = Mathf.Round(screen.y);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out Vector2 local))
                    vertex.position = new Vector3(local.x, local.y, vertex.position.z);
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
