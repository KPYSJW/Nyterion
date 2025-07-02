using System;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory;

namespace Nytherion.Core.Interfaces
{
    public interface IInventoryManager
    {
        bool AddItem(ItemData item);
        bool AddItem(ItemData item, int count);
        bool RemoveItem(ItemData item);
        bool RemoveItem(ItemData item, int count);
        void SwapItems(InventorySlotUI fromSlot, InventorySlotUI toSlot);
        
        bool HasItem(ItemData item);
        bool HasItem(string itemId);
        int GetItemCount(ItemData item);
        bool IsFull { get; }
        void ClearInventory();
        event Action<ItemData, int> OnItemAdded;
        event Action<ItemData, int> OnItemRemoved;
        event Action OnInventoryUpdated;
    }
}