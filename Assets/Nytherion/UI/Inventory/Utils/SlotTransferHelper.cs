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
            if (source == null || source.IsEmpty)
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