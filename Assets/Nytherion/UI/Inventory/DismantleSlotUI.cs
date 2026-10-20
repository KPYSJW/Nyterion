using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.UI.Components;

namespace Nytherion.UI.Inventory
{
    // 분해 대기 슬롯은 표시와 툴팁만 담당한다. 장비 이동은 분해 패널이 처리한다.
    public class DismantleSlotUI : BaseSlotUI
    {
        public override bool CanReceiveItem(Nytherion.Data.ScriptableObjects.Items.ItemData item) => false;
        public override void OnBeginDrag(PointerEventData eventData) { }
        public override void OnDrag(PointerEventData eventData) { }
        public override void OnEndDrag(PointerEventData eventData) { }
        protected override void UpdateVisuals(ItemData item, int count)
        {
            if (iconImage != null && item != null)
                PixelPerfectItemIcon.Apply(iconImage, transform as RectTransform,
                    item is WeaponData weapon ? weapon.inventoryIconScale : 1f, item is WeaponData,
                    item is WeaponData weaponData ? weaponData.inventoryIconSizeMultiplier : 1f);
            base.UpdateVisuals(item, count);
        }
        protected override void UpdateSlotBackground(ItemData item, bool hasItem)
        {
            base.UpdateSlotBackground(item, hasItem);
            PixelPerfectSlotFrame.Apply(slotBackgroundImage != null ? slotBackgroundImage : GetComponent<UnityEngine.UI.Image>());
        }
        protected override void ApplyIconLayout()
        {
            if (iconImage != null) iconImage.GetComponent<Nytherion.UI.Components.PixelPerfectItemIcon>()?.Refresh();
        }
    }
}
