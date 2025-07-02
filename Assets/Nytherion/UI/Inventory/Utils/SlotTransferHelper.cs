using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Inventory.Utils
{
    public static class SlotTransferHelper
    {
        public static bool CanTransferItem(BaseSlotUI source, BaseSlotUI target)
        {
            if (source == null || target == null || source.IsEmpty)
                return false;
                
            if (!target.CanReceiveItem(source.CurrentItem))
                return false;
                
            if (source is EquipmentSlotUI sourceEquipment && target is EquipmentSlotUI targetEquipment)
            {
                return sourceEquipment.SlotType == targetEquipment.SlotType;
            }
            
            if (source is EquipmentSlotUI && target is InventorySlotUI)
            {
                return true;
            }
            
            if (source is InventorySlotUI && target is EquipmentSlotUI targetEquipSlot)
            {
                return source.CurrentItem.itemType == targetEquipSlot.SlotType;
            }
            
            
            return true;
        }
        
        public static void TransferItem(BaseSlotUI source, BaseSlotUI target)
        {
            if (!CanTransferItem(source, target) || source.IsEmpty)
                return;

            if (target.IsEmpty)
            {
                target.SetItem(source.CurrentItem, source.CurrentCount);
                source.ClearSlot();
            }
            else
            {
                if (source.CurrentItem == target.CurrentItem)
                    return;
                    
                var tempItem = target.CurrentItem;
                var tempCount = target.CurrentCount;
                
                target.SetItem(source.CurrentItem, source.CurrentCount);
                source.SetItem(tempItem, tempCount);
            }
        }
        
        public static void HandleDropOnEmptySpace(BaseSlotUI source, PointerEventData eventData)
        {
            if (source == null || source.IsEmpty)
                return;
                
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                // TODO: 아이템 버리기 로직 구현 (필요시)
                Debug.Log($"[SlotTransfer] 아이템 버림: {source.CurrentItem.itemName}");
                source.ClearSlot();
            }
        }
    }
}
