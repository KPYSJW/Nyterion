using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory;

namespace Nytherion.Interfaces
{
    public interface IInventoryManager
    {
        bool AddItem(ItemData item);
        bool RemoveItem(ItemData item);
        void SwapItems(InventorySlotUI fromSlot, InventorySlotUI toSlot);
        bool HasItem(ItemData item);
        bool HasItem(string itemId);
        int GetItemCount(ItemData item);
        bool IsFull { get; }
        void ClearInventory();
        event Action<ItemData> OnItemAdded;
        event Action OnInventoryUpdated;
    }
}