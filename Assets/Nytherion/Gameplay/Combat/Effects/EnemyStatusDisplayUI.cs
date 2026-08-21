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
        private float offsetY = 1.0f;

        [Header("Icon Display Settings")]
        [Tooltip("아이콘 간의 가로 간격")]
        [SerializeField] private float iconSpacing = 0.25f;
        
        [Tooltip("아이콘 크기 조절")]
        [SerializeField] private Vector3 iconScale = new Vector3(0.25f, 0.25f, 1f);
        
        [Tooltip("머리 위 추가 높이 오프셋")]
        [SerializeField] private float heightExtraOffset = 0f;

        public void Initialize(SpriteRenderer spriteRenderer)
        {
            enemySpriteRenderer = spriteRenderer;
            if (enemySpriteRenderer != null)
            {
                offsetY = enemySpriteRenderer.bounds.extents.y + heightExtraOffset;
            }

            for (int i = 0; i < SupportedEffectIds.Length; i++)
            {
                GetOrCreateIcon(SupportedEffectIds[i]).gameObject.SetActive(false);
            }
        }

        public void UpdateDisplay(List<StatusEffect> activeEffects)
        {
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

            int count = 0;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i].EffectIcon != null) count++;
            }

            if (count == 0) return;

            float startX = -((count - 1) * iconSpacing) / 2f;
            int visibleIndex = 0;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                StatusEffect effect = activeEffects[i];
                if (effect.EffectIcon == null) continue;

                SpriteRenderer iconRenderer = GetOrCreateIcon(effect.EffectId);
                iconRenderer.sprite = effect.EffectIcon;
                iconRenderer.gameObject.SetActive(true);
                float posX = startX + (visibleIndex * iconSpacing);
                iconRenderer.transform.localPosition = new Vector3(posX, offsetY, 0f);
                visibleIndex++;
            }
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
