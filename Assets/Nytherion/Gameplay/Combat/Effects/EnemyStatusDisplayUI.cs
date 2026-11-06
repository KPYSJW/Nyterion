using UnityEngine;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Combat
{
    public class EnemyStatusDisplayUI : MonoBehaviour
    {
        private static readonly string[] SupportedEffectIds =
        {
            "Fire", "Ice", "Lightning", "Poison", "Curse", "Holy", "Demonic"
        };

        private SpriteRenderer enemySpriteRenderer;
        private readonly Dictionary<string, SpriteRenderer> iconRenderers = new Dictionary<string, SpriteRenderer>();
        private readonly List<SpriteRenderer> visibleIcons = new List<SpriteRenderer>();
        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();

        [Header("Icon Display Settings")]
        [Tooltip("적 크기와 무관하게 유지되는 아이콘 간의 월드 좌표 간격")]
        [SerializeField] private float iconSpacing = 0.25f;
        
        [Tooltip("적의 확대·좌우 반전 배율을 제외한 아이콘 크기")]
        [SerializeField] private Vector3 iconScale = new Vector3(0.25f, 0.25f, 1f);
        
        [Tooltip("머리 위 추가 높이 오프셋")]
        [SerializeField] private float heightExtraOffset = 0.03f;

        public void Initialize(SpriteRenderer spriteRenderer)
        {
            enemySpriteRenderer = spriteRenderer;
            for (int i = 0; i < SupportedEffectIds.Length; i++)
            {
                GetOrCreateIcon(SupportedEffectIds[i]).gameObject.SetActive(false);
            }
        }

        public void UpdateDisplay(List<StatusEffect> activeEffects)
        {
            visibleIcons.Clear();
            foreach (KeyValuePair<string, SpriteRenderer> pair in iconRenderers)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }

            if (activeEffects == null || activeEffects.Count == 0)
            {
                return;
            }

            for (int i = 0; i < activeEffects.Count; i++)
            {
                StatusEffect effect = activeEffects[i];
                if (effect.EffectIcon == null) continue;

                SpriteRenderer iconRenderer = GetOrCreateIcon(effect.EffectId);
                iconRenderer.sprite = effect.EffectIcon;
                iconRenderer.gameObject.SetActive(true);
                visibleIcons.Add(iconRenderer);
            }
            UpdateLayout();
        }

        private void LateUpdate() => UpdateLayout();

        private void UpdateLayout()
        {
            if (visibleIcons.Count == 0 || enemySpriteRenderer == null) return;
            bool bodyVisible = enemySpriteRenderer.enabled && enemySpriteRenderer.sprite != null;
            if (!bodyVisible)
            {
                foreach (SpriteRenderer icon in visibleIcons) icon.enabled = false;
                return;
            }

            Bounds bounds = bodyBounds.GetBounds(enemySpriteRenderer);
            float startX = -((visibleIcons.Count - 1) * iconSpacing) / 2f;
            for (int i = 0; i < visibleIcons.Count; i++)
            {
                SpriteRenderer icon = visibleIcons[i];
                Vector3 parentScale = icon.transform.parent.lossyScale;
                icon.transform.localScale = new Vector3(iconScale.x / SafeScale(parentScale.x),
                    iconScale.y / SafeScale(parentScale.y), iconScale.z / SafeScale(parentScale.z));
                float halfHeight = icon.sprite.bounds.extents.y * Mathf.Abs(iconScale.y);
                // 월드 좌표로 머리 바로 위에 배치해 부모 배율이 위치에 다시 곱해지지 않게 합니다.
                icon.transform.SetPositionAndRotation(new Vector3(bounds.center.x + startX + i * iconSpacing,
                    bounds.max.y + heightExtraOffset + halfHeight, enemySpriteRenderer.transform.position.z), Quaternion.identity);
                icon.sortingLayerID = enemySpriteRenderer.sortingLayerID;
                icon.sortingOrder = enemySpriteRenderer.sortingOrder + 10;
                icon.enabled = true;
            }
        }

        private static float SafeScale(float value)
        {
            return Mathf.Abs(value) < 0.001f ? 0.001f : value;
        }

        private SpriteRenderer GetOrCreateIcon(string effectId)
        {
            if (iconRenderers.TryGetValue(effectId, out SpriteRenderer cachedRenderer) && cachedRenderer != null)
            {
                return cachedRenderer;
            }

            GameObject iconObject = new GameObject("StatusIcon_" + effectId);
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localScale = iconScale;

            SpriteRenderer iconRenderer = iconObject.AddComponent<SpriteRenderer>();
            if (enemySpriteRenderer != null)
            {
                iconRenderer.sortingLayerID = enemySpriteRenderer.sortingLayerID;
                iconRenderer.sortingOrder = enemySpriteRenderer.sortingOrder + 10;
            }
            else
            {
                iconRenderer.sortingOrder = 10;
            }

            iconRenderers[effectId] = iconRenderer;
            return iconRenderer;
        }
    }
}
