using Nytherion.Core.Enums;
using Nytherion.UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.RelicBoard
{
    /// <summary>인벤토리와 같은 등급별 프레임을 사용하고 빈 슬롯·배치 강조 상태를 복원한다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public class RelicSlotVisual : MonoBehaviour
    {
        [SerializeField] private Sprite defaultSlotSprite;
        [SerializeField] private Sprite commonSlotSprite;
        [SerializeField] private Sprite uncommonSlotSprite;
        [SerializeField] private Sprite rareSlotSprite;
        [SerializeField] private Sprite epicSlotSprite;
        [SerializeField] private Sprite legendarySlotSprite;
        private Image frame;
        private Color originalColor;

        private void EnsureFrame()
        {
            if (frame != null) return;
            frame = GetComponent<Image>();
            originalColor = frame.color;
        }

        public void SetRarity(Rarity? rarity)
        {
            EnsureFrame();
            Sprite sprite = rarity switch
            {
                Rarity.Common => commonSlotSprite,
                Rarity.Uncommon => uncommonSlotSprite,
                Rarity.Rare => rareSlotSprite,
                Rarity.Epic => epicSlotSprite,
                Rarity.Legendary => legendarySlotSprite,
                _ => defaultSlotSprite
            };
            frame.sprite = sprite != null ? sprite : defaultSlotSprite;
            PixelPerfectSlotFrame.Apply(frame);
        }

        public void Highlight(bool active)
        {
            EnsureFrame();
            frame.color = active ? Color.yellow : originalColor;
        }
    }
}
