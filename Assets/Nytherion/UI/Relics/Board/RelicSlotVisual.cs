using Nytherion.Core.Enums;
using Nytherion.UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.RelicBoard
{
    /// <summary>인벤토리와 같은 등급별 프레임을 사용하고 빈 슬롯의 기본 프레임을 복원한다.</summary>
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

        private void EnsureFrame()
        {
            if (frame != null) return;
            frame = GetComponent<Image>();
        }

        public Sprite GetRaritySprite(Rarity? rarity)
        {
            Sprite sprite = rarity switch
            {
                Rarity.Common => commonSlotSprite,
                Rarity.Uncommon => uncommonSlotSprite,
                Rarity.Rare => rareSlotSprite,
                Rarity.Epic => epicSlotSprite,
                Rarity.Legendary => legendarySlotSprite,
                _ => defaultSlotSprite
            };
            return sprite != null ? sprite : defaultSlotSprite;
        }

        public void SetRarity(Rarity? rarity)
        {
            EnsureFrame();
            frame.sprite = GetRaritySprite(rarity);
            PixelPerfectSlotFrame.Apply(frame);
        }
    }
}
