using System;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.Core.Systems
{
    /// <summary>
    /// 인벤토리의 데이터 상태를 관리하는 클래스
    /// 아이템의 추가, 삭제, 이동 등 핵심 데이터 조작만 담당
    /// </summary>
    public class InventoryModel
    {
        /// <summary> 인벤토리 데이터가 변경될 때마다 호출/// </summary>
        public event Action OnInventoryUpdated;

        // 아이템 데이터와 수량을 쌍으로 가지는 배열로 인벤토리 슬롯을 관리
        private readonly (ItemData item, int count)[] slots;

        /// <summary> 인벤토리의 최대 슬롯 개수/// </summary>
        public int MaxSlots => slots.Length;

        ///<summary>인벤토리가 가득 찼는지 여부 </summary>
        public bool IsFull => GetFirstEmptySlot() == -1;

        /// <summary>빈 슬롯이 하나라도 존재하는지 여부/// </summary>
        public bool HasEmptySlot => GetFirstEmptySlot() != -1;

        /// <summary>
        /// 지정된 크기로 인벤토리 모델을 초기화
        /// </summary>
        /// <param name="maxSlots">새성할 최대 슬롯 수</param>
        public InventoryModel(int maxSlots)
        {
            slots = new (ItemData, int)[maxSlots];
        }

        /// <summary>
        /// 특정 인덱스의 슬롯에 있는 아이템 정보를 반환
        /// </summary>
        public (ItemData item, int count) GetItemAt(int index)
        {
            if (index < 0 || index >= slots.Length) return (null, 0);
            return slots[index];
        }

        /// <summary>
        /// 인벤토리에 아이템을 추가
        /// 중첩 가능한 아이템(Stackable)인 경우 기존 슬롯에 합치고, 그렇지 않으면 빈 슬롯에 추가
        /// </summary>
        /// <returns>추가 성공 여부</returns>
        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            // 중첩 가능한 아이템 처리
            if (item.isStackable)
            {
                int existingSlot = FindSlotWithItem(item.ID);
                if (existingSlot != -1)
                {
                    slots[existingSlot].count += count;
                    OnInventoryUpdated?.Invoke();
                    return true;
                }
            }

            // 빈 슬롯 찾아서 추가
            int emptySlot = GetFirstEmptySlot();
            if (emptySlot != -1)
            {
                slots[emptySlot] = (item, count);
                OnInventoryUpdated?.Invoke();
                return true;
            }

            // 인벤토리에 빈 공간이 없는 경우
            return false;
        }

        /// <summary>
        /// 특정 슬롯에 아이템을 강제로 할당. (기존 아이템 덮어쓰기 가능)
        /// </summary>
        public void AddItemToSlot(ItemData item, int count, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            slots[slotIndex] = (item, count);
            OnInventoryUpdated?.Invoke();
        }

        /// <summary>
        /// 특정 슬롯에 아이템을 할당하되, 덮어쓰기 옵션을 제어
        /// </summary>
        /// <param name="overwrite">true일 경우 슬롯에 아이템이 있어도 덮어쓴다</param>
        /// <returns>할당 성공 여부</returns>
        public bool AddItemToSlot(ItemData item, int count, int slotIndex, bool overwrite)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return false;
            if (!overwrite && slots[slotIndex].item != null) return false; // 덮어쓰기 불가이고 슬롯이 차있으면 실패

            slots[slotIndex] = (item, count);
            OnInventoryUpdated?.Invoke();
            return true;
        }

        /// <summary>
        /// 지정된 아이템을 인벤토리에서 검색하여 수량만큼 제거
        /// </summary>
        /// <returns>제거 성공 여부 (수량이 부족하면 실패)</returns>
        public bool RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].item != null && slots[i].item.ID == item.ID)
                {
                    if (slots[i].count >= count)
                    {
                        slots[i].count -= count;
                        if (slots[i].count <= 0)
                        {
                            slots[i] = (null, 0); // 수량이 0이하가 되면 슬롯을 비움
                        }
                        OnInventoryUpdated?.Invoke();
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 특정 인덱스의 슬롯에서 수량만큼 아이템을 제거
        /// </summary>
        public void RemoveItemFromSlot(int slotIndex, int count)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex].item == null) return;

            slots[slotIndex].count -= count;
            if (slots[slotIndex].count <= 0)
            {
                slots[slotIndex] = (null, 0);
            }
            OnInventoryUpdated?.Invoke();
        }

        /// <summary>
        /// 두 슬롯의 아이템을 서로 교환 (드래그 앤 드롭 등에서 사용)
        /// </summary>
        public void SwapItems(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= slots.Length || toIndex < 0 || toIndex >= slots.Length) return;

            var temp = slots[fromIndex];
            slots[fromIndex] = slots[toIndex];
            slots[toIndex] = temp;
            OnInventoryUpdated?.Invoke();
        }

        /// <summary>
        /// 특정 슬롯에서 아이템을 제거하되, 이벤트 알림(OnInventoryUpdated)을 발생시키지 않고 제거된 데이터를 반환
        /// </summary>
        public (ItemData item, int count) RemoveItemFromSlotWithoutNotify(int slotIndex, int count)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex].item == null) return (null, 0);

            var removedItem = slots[slotIndex];

            slots[slotIndex].count -= count;
            if (slots[slotIndex].count <= 0)
            {
                slots[slotIndex] = (null, 0);
            }

            return (removedItem.item, count);
        }

        /// <summary>
        /// 인벤토리의 모든 슬롯은 비우는 메서드
        /// </summary>
        public void Clear()
        {
            Array.Clear(slots, 0, slots.Length);
            OnInventoryUpdated?.Invoke();
        }

        /// <summary>
        /// 비어있는 가장 첫 번째 슬롯의 인덱스를 반환
        /// </summary>
        public int GetFirstEmptySlot()
        {
            return Array.FindIndex(slots, s => s.item == null);
        }

        /// <summary>
        /// 특정 아이템 ID를 가진 아이템이 존재하는 첫 번째 슬롯의 인덱스를 반환
        /// </summary>
        private int FindSlotWithItem(string itemID)
        {
            return Array.FindIndex(slots, s => s.item != null && s.item.ID == itemID);
        }
    }
}