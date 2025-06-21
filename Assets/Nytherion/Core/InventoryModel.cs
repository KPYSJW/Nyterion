using System;
using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.Core
{
    public class InventoryModel
    {
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action OnInventoryUpdated;

        private readonly Dictionary<ItemData, int> items = new();
        public IReadOnlyDictionary<ItemData, int> Items => items;

        private readonly int maxSlots;
        public bool IsFull => items.Count >= maxSlots;
        public bool HasEmptySlot => items.Count < maxSlots;

        public InventoryModel(int maxSlots)
        {
            this.maxSlots = maxSlots;
        }

        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            if (items.TryGetValue(item, out int currentCount))
            {
                if (item.isStackable)
                {
                    items[item] = currentCount + count;
                }
                else
                {
                    if (IsFull) return false;
                    items[item] = count;
                }
            }
            else
            {
                if (IsFull) return false;
                items[item] = count;
            }

            OnItemAdded?.Invoke(item, count);
            OnInventoryUpdated?.Invoke();
            return true;
        }

        public bool RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0 || !items.ContainsKey(item)) return false;

            if (items[item] > count)
            {
                items[item] -= count;
            }
            else
            {
                items.Remove(item);
            }

            OnItemRemoved?.Invoke(item, count);
            OnInventoryUpdated?.Invoke();
            return true;
        }

        public int GetItemCount(ItemData item)
        {
            items.TryGetValue(item, out int count);
            return count;
        }

        public bool HasItem(ItemData item)
        {
            return items.ContainsKey(item);
        }

        public void Clear()
        {
            items.Clear();
            OnInventoryUpdated?.Invoke();
        }
        
        public void SwapItems(ItemData fromItem, ItemData toItem)
        {
            if (fromItem == null || !items.ContainsKey(fromItem)) return;
            if (toItem == null) return; 

            if(!items.ContainsKey(toItem)) return;

            int fromCount = items[fromItem];
            int toCount = items[toItem];

            items[fromItem] = toCount;
            items[toItem] = fromCount;
            
            OnInventoryUpdated?.Invoke();
        }
    }
}