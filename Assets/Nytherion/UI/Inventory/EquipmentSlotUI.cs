using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using ItemType = Nytherion.Data.ScriptableObjects.Items.ItemType;

namespace Nytherion.UI.Inventory
{
    public class EquipmentSlotUI : BaseSlotUI
    {
        [SerializeField] private ItemType slotType = ItemType.Weapon; // 기본값을 Weapon으로 설정
        public ItemType SlotType => slotType;
        
        protected override void Awake()
        {
            base.Awake();
            OnBeginDragEvent += (s, e) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleBeginDragShared(s);
            OnEndDragEvent += (s, e) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleEndDragShared(s, e);
            OnPointerClickEvent += HandlePointerClick;
        }

        public override void SetItem(ItemData newItem, int count = 1)
        {
            if (currentItem == newItem || (newItem == null && currentItem == null))
                return;

            if (newItem != null && newItem.itemType != slotType)
            {
                Debug.LogWarning($"[EquipmentSlot] Cannot equip {newItem.itemName} to {slotType} slot");
                return;
            }
            
            base.SetItem(newItem, count);
        }
        
        public override bool CanReceiveItem(ItemData item)
        {
            return item == null || item.itemType == slotType;
        }

        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && !IsEmpty)
            {
                UnequipItem();
            }
        }

        private void UnequipItem()
        {
            if (IsEmpty) return;
            
            if (InventoryManager.Instance.AddItem(currentItem, 1))
            {
                ClearSlot();
                var inventoryUI = FindObjectOfType<InventoryUI>();
                if (inventoryUI != null)
                {
                    inventoryUI.RefreshUI();
                }
            }
        }

        private void SwapEquipment(EquipmentSlotUI otherSlot)
        {
            ItemData tempItem = currentItem;
            int tempCount = currentCount;
            
            SetItem(otherSlot.currentItem, otherSlot.currentCount);
            otherSlot.SetItem(tempItem, tempCount);
        }
    }
}
