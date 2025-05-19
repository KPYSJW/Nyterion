using UnityEngine;
using Nytherion.UI.Inventory;

namespace Nytherion.Utils
{
    /// <summary>
    /// 슬롯 간 아이템 이동을 처리하는 헬퍼 클래스
    /// </summary>
    public static class SlotTransferHelper
    {
        /// <summary>
        /// 출발 슬롯에서 도착 슬롯으로 아이템을 이동시킵니다.
        /// </summary>
        /// <param name="fromSlot">아이템을 가져올 슬롯</param>
        /// <param name="toSlot">아이템을 넣을 슬롯</param>
        /// <returns>이동 성공 여부</returns>
        public static bool TryTransferItem(BaseSlotUI fromSlot, BaseSlotUI toSlot)
        {
            if (fromSlot == null || fromSlot.IsEmpty || toSlot == null)
            {
                Debug.Log("[SlotTransfer] 유효하지 않은 슬롯 또는 빈 슬롯입니다.");
                return false;
            }

            var item = fromSlot.Item;
            var count = fromSlot.StackCount;

            // 같은 슬롯이면 무시
            if (fromSlot == toSlot)
            {
                Debug.Log($"[SlotTransfer] 동일한 슬롯입니다: {item?.itemName}");
                return false;
            }

            // 같은 아이템이 이미 있는 경우 스택 병합 시도
            if (toSlot.HasItem(item))
            {
                // 스택 가능한 아이템인지 확인
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

            // 대상 슬롯이 비어있는 경우 이동
            if (toSlot.IsEmpty)
            {
                toSlot.SetItem(item, count);
                fromSlot.ClearSlot();
                Debug.Log($"[SlotTransfer] 아이템 이동: {item.itemName} x{count}");
                return true;
            }

            // 여기까지 왔다면 대상 슬롯에 다른 아이템이 있는 경우
            Debug.Log($"[SlotTransfer] 대상 슬롯에 다른 아이템이 있습니다: {toSlot.Item?.itemName}");
            return false;
        }

        /// <summary>
        /// 두 슬롯의 아이템을 교환합니다.
        /// </summary>
        /// <param name="slotA">교환할 첫 번째 슬롯</param>
        /// <param name="slotB">교환할 두 번째 슬롯</param>
        /// <returns>교환 성공 여부</returns>
        public static bool TrySwapItems(BaseSlotUI slotA, BaseSlotUI slotB)
        {
            if (slotA == null || slotB == null || slotA.IsEmpty || slotB.IsEmpty)
                return false;

            // 두 슬롯 모두 아이템이 있는 경우에만 교환
            var itemA = slotA.Item;
            var countA = slotA.StackCount;
            
            var itemB = slotB.Item;
            var countB = slotB.StackCount;

            try
            {
                // 먼저 두 슬롯을 비웁니다.
                slotA.ClearSlot();
                slotB.ClearSlot();

                // 아이템을 서로 교환하여 설정합니다.
                // 콜백은 각 슬롯의 기본 동작에 맡깁니다.
                slotA.SetItem(itemB, countB);
                slotB.SetItem(itemA, countA);

                Debug.Log($"[SlotTransfer] 아이템 교환: {itemA?.itemName} ↔ {itemB?.itemName}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SlotTransfer] 아이템 교환 중 오류 발생: {e.Message}");
                
                // 오류 발생 시 원상복구 시도
                slotA.ClearSlot();
                slotB.ClearSlot();
                
                slotA.SetItem(itemA, countA);
                slotB.SetItem(itemB, countB);
                
                return false;
            }
        }
    }
}
