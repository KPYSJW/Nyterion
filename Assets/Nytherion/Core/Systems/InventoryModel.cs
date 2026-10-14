using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
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
        private (ItemData item, int count)[] slots;
        // 분해 대기 장비는 원래 슬롯을 예약한다. 저장에는 남기고 일반 조회에서는 숨긴다.
        private readonly List<int> dismantleSlots = new List<int>();
        public bool IsDismantleReserved(int index) => dismantleSlots.Contains(index);
        public IReadOnlyList<int> DismantleSlots => dismantleSlots;

        public (ItemData item, int count) GetStoredItemAt(int index)
        {
            return index >= 0 && index < slots.Length ? slots[index] : (null, 0);
        }

        public bool StageForDismantle(int index, ItemData expectedItem)
        {
            var slot = GetItemAt(index);
            if (!(slot.item is EquipmentData) || slot.item != expectedItem || slot.count != 1 || IsDismantleReserved(index))
                return false;
            dismantleSlots.Add(index);
            OnInventoryUpdated?.Invoke();
            return true;
        }

        public void ResetDismantle()
        {
            if (dismantleSlots.Count == 0) return;
            dismantleSlots.Clear();
            OnInventoryUpdated?.Invoke();
        }

        // 재화 지급 전에 데이터만 확정해 자동 저장에 장비와 골드가 함께 반영되게 한다.
        public void CompleteDismantleWithoutNotify()
        {
            foreach (int index in dismantleSlots) slots[index] = (null, 0);
            dismantleSlots.Clear();
        }

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

        /// <summary> 기존 아이템과 슬롯 위치를 유지하면서 필요한 만큼 확장한다. </summary>
        public void EnsureSlotCapacity(int requiredSlots)
        {
            if (requiredSlots <= MaxSlots) return;

            Array.Resize(ref slots, requiredSlots);
            OnInventoryUpdated?.Invoke();
        }

        /// <summary>
        /// 특정 인덱스의 슬롯에 있는 아이템 정보를 반환
        /// </summary>
        public (ItemData item, int count) GetItemAt(int index)
        {
            if (index < 0 || index >= slots.Length || IsDismantleReserved(index)) return (null, 0);
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
            if (slotIndex < 0 || slotIndex >= slots.Length || IsDismantleReserved(slotIndex)) return;
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
            if (slotIndex < 0 || slotIndex >= slots.Length || IsDismantleReserved(slotIndex)) return false;
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
                if (!IsDismantleReserved(i) && slots[i].item != null && slots[i].item.ID == item.ID)
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
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex].item == null || IsDismantleReserved(slotIndex)) return;

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
            if (IsDismantleReserved(fromIndex) || IsDismantleReserved(toIndex)) return;

            var temp = slots[fromIndex];
            slots[fromIndex] = slots[toIndex];
            slots[toIndex] = temp;
            OnInventoryUpdated?.Invoke();
        }

        /// <summary> 높은 등급, 이름순으로 정렬하고 빈 슬롯은 뒤로 보낸다. </summary>
        public bool SortByRarityAndName()
        {
            var occupiedSlots = new List<(ItemData item, int count, int originalIndex)>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (!IsDismantleReserved(i) && slots[i].item != null)
                    occupiedSlots.Add((slots[i].item, slots[i].count, i));
            }

            occupiedSlots.Sort((left, right) =>
            {
                Rarity leftRarity = left.item is EquipmentData leftEquipment ? leftEquipment.rarity : Rarity.Common;
                Rarity rightRarity = right.item is EquipmentData rightEquipment ? rightEquipment.rarity : Rarity.Common;
                int comparison = rightRarity.CompareTo(leftRarity);
                if (comparison != 0) return comparison;

                comparison = StringComparer.OrdinalIgnoreCase.Compare(GetSortName(left.item), GetSortName(right.item));
                // 같은 이름의 장비와 분리된 스택은 기존 순서를 유지한다.
                return comparison != 0 ? comparison : left.originalIndex.CompareTo(right.originalIndex);
            });

            bool changed = false;
            int targetIndex = 0;
            for (int i = 0; i < occupiedSlots.Count; i++)
            {
                while (IsDismantleReserved(targetIndex)) targetIndex++;
                if (occupiedSlots[i].originalIndex != targetIndex++)
                {
                    changed = true;
                    break;
                }
            }
            if (!changed) return false;

            // 기존 인스턴스와 수량을 그대로 옮기고, 정렬 완료 후 한 번만 갱신한다.
            for (int i = 0; i < slots.Length; i++)
                if (!IsDismantleReserved(i)) slots[i] = (null, 0);
            targetIndex = 0;
            for (int i = 0; i < occupiedSlots.Count; i++)
            {
                while (IsDismantleReserved(targetIndex)) targetIndex++;
                slots[targetIndex++] = (occupiedSlots[i].item, occupiedSlots[i].count);
            }
            OnInventoryUpdated?.Invoke();
            return true;
        }

        private static string GetSortName(ItemData item)
        {
            if (!string.IsNullOrWhiteSpace(item.itemName_EN)) return item.itemName_EN.Trim();
            if (!string.IsNullOrWhiteSpace(item.itemName_KR)) return item.itemName_KR.Trim();
            return item.ID ?? string.Empty;
        }

        /// <summary>
        /// 특정 슬롯에서 아이템을 제거하되, 이벤트 알림(OnInventoryUpdated)을 발생시키지 않고 제거된 데이터를 반환
        /// </summary>
        public (ItemData item, int count) RemoveItemFromSlotWithoutNotify(int slotIndex, int count)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex].item == null || IsDismantleReserved(slotIndex)) return (null, 0);

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
            dismantleSlots.Clear();
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
            for (int i = 0; i < slots.Length; i++)
                if (!IsDismantleReserved(i) && slots[i].item != null && slots[i].item.ID == itemID) return i;
            return -1;
        }
    }
}
