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
            // 드래그 앤 드롭 이벤트 설정
            OnBeginDragEvent += (s, e) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleBeginDragShared(s);
            OnEndDragEvent += (s, e) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleEndDragShared(s, e);
            OnPointerClickEvent += HandlePointerClick;
        }

        public override void SetItem(ItemData newItem, int count = 1)
        {
            // 이미 같은 아이템이 설정되어 있거나, 새 아이템이 null이면 무시
            if (currentItem == newItem || (newItem == null && currentItem == null))
                return;

            // 새 아이템이 있고, 타입이 일치하지 않으면 설정하지 않음
            if (newItem != null && newItem.itemType != slotType)
            {
                Debug.LogWarning($"[EquipmentSlot] Cannot equip {newItem.itemName} to {slotType} slot");
                return;
            }
            
            base.SetItem(newItem, count);
        }
        
        // 드래그 앤 드롭 시 타겟 슬롯으로 아이템을 설정하기 전에 호출되는 메서드
        public override bool CanReceiveItem(ItemData item)
        {
            // 아이템이 없거나, 타입이 일치하면 true 반환
            return item == null || item.itemType == slotType;
        }

        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && !IsEmpty)
            {
                // 장비 해제
                UnequipItem();
            }
        }

        private void UnequipItem()
        {
            if (IsEmpty) return;
            
            // 인벤토리에 아이템 추가 시도
            if (InventoryManager.Instance.AddItem(currentItem, 1))
            {
                ClearSlot();
                // 인벤토리 UI 갱신
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
