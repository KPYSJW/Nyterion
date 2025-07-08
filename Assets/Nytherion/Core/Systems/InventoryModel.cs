using System;
using System.Collections.Generic;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.Core.Systems
{
    public class InventoryModel
    {
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action OnInventoryUpdated;

        private readonly List<ItemData> items = new List<ItemData>();
        public IReadOnlyList<ItemData> Items => items;

        private readonly int maxSlots;
        public bool IsFull => items.Count(item => !item.isStackable) + items.Where(item => item.isStackable).Select(item => item.ID).Distinct().Count() >= maxSlots;
        public bool HasEmptySlot => !IsFull;

        public InventoryModel(int maxSlots)
        {
            this.maxSlots = maxSlots;
        }

        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;
            
            for (int i = 0; i < count; i++)
            {
                 if (IsFull) return false;
                 items.Add(item);
            }

            OnItemAdded?.Invoke(item, count);
            OnInventoryUpdated?.Invoke();
            return true;
        }

        public bool RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            int removedCount = 0;
            for (int i = 0; i < count; i++)
            {
                ItemData itemToRemove;
                if (!item.isStackable && !string.IsNullOrEmpty(item.instanceId))
                {
                    itemToRemove = items.FirstOrDefault(x => x.instanceId == item.instanceId);
                }
                else
                {
                    itemToRemove = items.FirstOrDefault(x => x.ID == item.ID);
                }

                if (itemToRemove != null)
                {
                    items.Remove(itemToRemove);
                    removedCount++;
                }
                else
                {
                    break; 
                }
            }

            if (removedCount > 0)
            {
                OnItemRemoved?.Invoke(item, removedCount);
                OnInventoryUpdated?.Invoke();
            }
            
            return removedCount > 0;
        }

        public int GetItemCount(ItemData item)
        {
            if (item == null) return 0;
            
            if (!item.isStackable && !string.IsNullOrEmpty(item.instanceId))
            {
                return items.Count(i => i.instanceId == item.instanceId);
            }
            return items.Count(i => i.ID == item.ID);
        }

        public bool HasItem(ItemData item)
        {
            if (item == null) return false;
            if (!item.isStackable && !string.IsNullOrEmpty(item.instanceId))
            {
                 return items.Any(i => i.instanceId == item.instanceId);
            }
            return items.Any(i => i.ID == item.ID);
        }

        public void Clear()
        {
            items.Clear();
            OnInventoryUpdated?.Invoke();
        }
    }
}