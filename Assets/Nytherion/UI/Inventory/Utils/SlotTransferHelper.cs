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
            {
                return false;
            }

            return target.CanReceiveItem(source.CurrentItem);
        }

        public static void TransferItem(BaseSlotUI source, BaseSlotUI target)
        {
            // 드롭 이벤트가 누락된 경우에도 장비 이동은 데이터 교환 경로만 사용한다.
            if (target is EquipmentSlotUI targetEquipment && source is InventorySlotUI sourceInventory)
            {
                targetEquipment.TryEquipFromInventory(sourceInventory);
                return;
            }
            if (source is EquipmentSlotUI sourceEquipment && target is InventorySlotUI targetInventory)
            {
                sourceEquipment.TryMoveToInventory(targetInventory);
                return;
            }
            if (source is EquipmentSlotUI || target is EquipmentSlotUI) return;

            if(source == target)
            {
                source.SetItem(source.CurrentItem, source.CurrentCount);
                return;
            }
            if (!CanTransferItem(source, target))
            {
                return;
            }

            if (target.IsEmpty)
            {
                target.SetItem(source.CurrentItem, source.CurrentCount);
                source.ClearSlot();
            }
            else
            {
                if (source.CurrentItem == target.CurrentItem && source.CurrentItem.isStackable)
                {
                    int totalAmount = source.CurrentCount + target.CurrentCount;
                    int maxStack = source.CurrentItem.maxStack;

                    if (totalAmount <= maxStack)
                    {
                        target.SetItem(source.CurrentItem, totalAmount);
                        source.ClearSlot();
                    }
                    else
                    {
                        target.SetItem(source.CurrentItem, maxStack);
                        source.SetItem(source.CurrentItem, totalAmount - maxStack);
                    }
                    return;
                }
                
                ItemData tempItem = target.CurrentItem;
                int tempCount = target.CurrentCount;

                target.SetItem(source.CurrentItem, source.CurrentCount);
                source.SetItem(tempItem, tempCount);
            }
        }

        public static void HandleDropOnEmptySpace(BaseSlotUI source, PointerEventData eventData)
        {
            if (source == null || source.IsEmpty || source is EquipmentSlotUI)
            {
                return;
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                source.ClearSlot();
            }
        }
    }
}
