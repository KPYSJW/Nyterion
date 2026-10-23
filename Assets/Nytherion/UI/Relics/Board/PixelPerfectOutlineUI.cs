using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.RelicBoard
{
    /// <summary>
    /// 세트·초월 아이콘은 64×64 UI 크기를 유지하고 Canvas 배율을 그대로 따른다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LayoutElement))]
    public sealed class PixelPerfectOutlineUI : MonoBehaviour
    {
        private const float BaseLogicalSize = 64f;
        private const float SizeComparisonEpsilon = 0.001f;

        private LayoutElement layoutElement;

        private void Awake()
        {
            layoutElement = GetComponent<LayoutElement>();
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

            if (Mathf.Abs(layoutElement.minWidth - BaseLogicalSize) < SizeComparisonEpsilon &&
                Mathf.Abs(layoutElement.minHeight - BaseLogicalSize) < SizeComparisonEpsilon &&
                Mathf.Abs(layoutElement.preferredWidth - BaseLogicalSize) < SizeComparisonEpsilon &&
                Mathf.Abs(layoutElement.preferredHeight - BaseLogicalSize) < SizeComparisonEpsilon)
            {
                return;
            }

            layoutElement.minWidth = BaseLogicalSize;
            layoutElement.minHeight = BaseLogicalSize;
            layoutElement.preferredWidth = BaseLogicalSize;
            layoutElement.preferredHeight = BaseLogicalSize;

            if (transform.parent is RectTransform parentRectTransform)
            {
                LayoutRebuilder.MarkLayoutForRebuild(parentRectTransform);
            }
        }
    }
}
