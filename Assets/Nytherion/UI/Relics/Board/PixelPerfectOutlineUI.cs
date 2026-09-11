using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.RelicBoard
{
    /// <summary>
    /// 픽셀 아트 아이콘 전체가 비정수 Canvas 배율에서 불균등하게 샘플링되지 않도록
    /// 실제 화면 크기를 원본 외곽선 단위에 맞춘다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LayoutElement))]
    public sealed class PixelPerfectOutlineUI : MonoBehaviour
    {
        private const float BaseLogicalSize = 64f;
        private const float ScreenPixelStep = 16f;
        private const float SizeComparisonEpsilon = 0.001f;

        private LayoutElement layoutElement;
        private Canvas rootCanvas;
        private float lastCanvasScaleFactor = -1f;

        private void Awake()
        {
            layoutElement = GetComponent<LayoutElement>();
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        private void OnEnable()
        {
            ApplyPixelPerfectSize();
        }

        private void LateUpdate()
        {
            ApplyPixelPerfectSize();
        }

        private void ApplyPixelPerfectSize()
        {
            if (layoutElement == null)
            {
                layoutElement = GetComponent<LayoutElement>();
            }

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            }

            if (rootCanvas == null || rootCanvas.scaleFactor <= 0f)
            {
                return;
            }

            float canvasScaleFactor = rootCanvas.scaleFactor;
            if (Mathf.Abs(lastCanvasScaleFactor - canvasScaleFactor) < SizeComparisonEpsilon)
            {
                return;
            }

            float desiredScreenSize = BaseLogicalSize * canvasScaleFactor;
            float snappedScreenSize = Mathf.Max(
                ScreenPixelStep,
                Mathf.Floor(desiredScreenSize / ScreenPixelStep + 0.5f) * ScreenPixelStep);
            float targetLogicalSize = snappedScreenSize / canvasScaleFactor;

            layoutElement.minWidth = targetLogicalSize;
            layoutElement.minHeight = targetLogicalSize;
            layoutElement.preferredWidth = targetLogicalSize;
            layoutElement.preferredHeight = targetLogicalSize;
            lastCanvasScaleFactor = canvasScaleFactor;

            if (transform.parent is RectTransform parentRectTransform)
            {
                LayoutRebuilder.MarkLayoutForRebuild(parentRectTransform);
            }
        }
    }
}
