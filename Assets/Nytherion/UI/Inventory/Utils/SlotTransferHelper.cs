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
                    // 스택 합치기 로직 
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
            if (source == null || source.IsEmpty)
            {
                return;
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Debug.Log($"[SlotTransfer] 아이템 버림: {source.CurrentItem.itemName}");
                source.ClearSlot();
            }
        }
    }
}