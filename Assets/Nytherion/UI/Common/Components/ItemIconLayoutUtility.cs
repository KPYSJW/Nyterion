using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.Components
{
    public static class ItemIconLayoutUtility
    {
        public static void Apply(Image image, Vector2 areaSize, bool diagonal, float iconScale, float rotationOffset = 0f)
        {
            if (image == null || image.sprite == null) return;

            RectTransform iconRect = image.rectTransform;
            Vector2 availableSize = iconRect.rect.size;
            Vector2 spriteSize = image.sprite.rect.size;
            if (availableSize.x <= 0f || availableSize.y <= 0f || areaSize.x <= 0f || areaSize.y <= 0f || spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            image.preserveAspect = true;
            image.raycastTarget = false;
            float angle = diagonal ? -45f + rotationOffset : 0f;
            iconRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            // 슬롯과 툴팁에서 같은 표시 크기 계산을 사용합니다.
            float fit = Mathf.Min(availableSize.x / spriteSize.x, availableSize.y / spriteSize.y);
            Vector2 displaySize = spriteSize * fit;
            float cos = Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad));
            float sin = Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 boundsSize = new Vector2(
                displaySize.x * cos + displaySize.y * sin,
                displaySize.x * sin + displaySize.y * cos);
            float scale = Mathf.Min(areaSize.x / boundsSize.x, areaSize.y / boundsSize.y) * Mathf.Max(0.1f, iconScale);
            iconRect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
