using UnityEngine;
using Nytherion.UI.Inventory;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.Utils
{
    public static class SlotTransferHelper
    {
        public static bool TryTransferItem(BaseSlotUI fromSlot, BaseSlotUI toSlot)
        {
            if (fromSlot == null || fromSlot.IsEmpty || toSlot == null)
            {
                Debug.Log("[SlotTransfer] 유효하지 않은 슬롯 또는 빈 슬롯입니다.");
                return false;
            }

            ItemData item = fromSlot.Item;
            int count = fromSlot.StackCount;

            if (fromSlot == toSlot)
            {
                Debug.Log($"[SlotTransfer] 동일한 슬롯입니다: {item?.itemName}");
                return false;
            }

            if (toSlot.HasItem(item))
            {
                if (item.isStackable)
                {
                    toSlot.IncreaseCount(count);
                    fromSlot.ClearSlot();
                    Debug.Log($"[SlotTransfer] 아이템 스택 병합: {item.itemName} x{count}");
                    return true;
                }
                
                Debug.Log($"[SlotTransfer] 스택 불가능한 아이템이 이미 있습니다: {item.itemName}");
                return false;
            }

            if (toSlot.IsEmpty)
            {
                toSlot.SetItem(item, count);
                fromSlot.ClearSlot();
                Debug.Log($"[SlotTransfer] 아이템 이동: {item.itemName} x{count}");
                return true;
            }

            Debug.Log($"[SlotTransfer] 대상 슬롯에 다른 아이템이 있습니다: {toSlot.Item?.itemName}");
            return false;
        }

        public static bool TrySwapItems(BaseSlotUI slotA, BaseSlotUI slotB)
        {
            if (slotA == null || slotB == null || slotA.IsEmpty || slotB.IsEmpty)
                return false;

            ItemData itemA = slotA.Item;
            int countA = slotA.StackCount;
            
            ItemData itemB = slotB.Item;
            int countB = slotB.StackCount;

            try
            {
                slotA.ClearSlot();
                slotB.ClearSlot();
                slotA.SetItem(itemB, countB);
                slotB.SetItem(itemA, countA);

                Debug.Log($"[SlotTransfer] 아이템 교환: {itemA?.itemName} ↔ {itemB?.itemName}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SlotTransfer] 아이템 교환 중 오류 발생: {e.Message}");
                
                slotA.ClearSlot();
                slotB.ClearSlot();
                
                slotA.SetItem(itemA, countA);
                slotB.SetItem(itemB, countB);
                
                return false;
            }
        }
    }
}
