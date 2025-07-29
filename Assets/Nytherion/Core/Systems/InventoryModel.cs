using System;
using System.Collections.Generic;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Items;
using UnityEngine;

namespace Nytherion.Core.Systems
{
    public class InventoryModel
    {
        public event Action OnInventoryUpdated;

        private readonly (ItemData item, int count)[] slots;
        public int MaxSlots => slots.Length;

        public bool IsFull => GetFirstEmptySlot() == -1;
        public bool HasEmptySlot => GetFirstEmptySlot() != -1;

        public InventoryModel(int maxSlots)
        {
            slots = new (ItemData, int)[maxSlots];
        }

        public (ItemData item, int count) GetItemAt(int index)
        {
            if (index < 0 || index >= slots.Length) return (null, 0);
            return slots[index];
        }

        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

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

            int emptySlot = GetFirstEmptySlot();
            if (emptySlot != -1)
            {
                slots[emptySlot] = (item, count);
                OnInventoryUpdated?.Invoke();
                return true;
            }

            return false;
        }

        public void AddItemToSlot(ItemData item, int count, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            slots[slotIndex] = (item, count);
            OnInventoryUpdated?.Invoke();
        }

        public bool AddItemToSlot(ItemData item, int count, int slotIndex, bool overwrite)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return false;
            if (!overwrite && slots[slotIndex].item != null) return false;

            slots[slotIndex] = (item, count);
            OnInventoryUpdated?.Invoke();
            return true;
        }


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
                            slots[i] = (null, 0);
                        }
                        OnInventoryUpdated?.Invoke();
                        return true;
                    }
                }
            }
            return false;
        }

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

        public void SwapItems(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= slots.Length || toIndex < 0 || toIndex >= slots.Length) return;

            var temp = slots[fromIndex];
            slots[fromIndex] = slots[toIndex];
            slots[toIndex] = temp;
            OnInventoryUpdated?.Invoke();
        }
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
        public void Clear()
        {
            Array.Clear(slots, 0, slots.Length);
            OnInventoryUpdated?.Invoke();
        }

        public int GetFirstEmptySlot()
        {
            return Array.FindIndex(slots, s => s.item == null);
        }

        private int FindSlotWithItem(string itemID)
        {
            return Array.FindIndex(slots, s => s.item != null && s.item.ID == itemID);
        }
    }
}