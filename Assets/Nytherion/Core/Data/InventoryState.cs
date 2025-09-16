using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.UI.Controllers;
using VContainer;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class InventoryState
    {
        [Serializable]
        public class ItemEntry
        {
            public string ItemId;
            public int Count;

            public ItemEntry() { }

            public ItemEntry(string itemId, int count)
            {
                ItemId = itemId;
                Count = count;
            }
        }

        private InventoryManager inventoryManager;
        private ShopUI shopUI;

        [Inject]
        public void Construct(InventoryManager inventoryManager, ShopUI shopUI)
        {
            this.inventoryManager = inventoryManager;
            this.shopUI = shopUI;
        }

        [SerializeField] private List<ItemEntry> items = new List<ItemEntry>();

        public IReadOnlyList<ItemEntry> Items => items;

        [Obsolete("Use Items property instead")]
        public IReadOnlyList<string> ItemIds => items.Select(entry => entry.ItemId).ToList();

        public InventoryState() { }

        public InventoryState(IEnumerable<ItemData> items) : this()
        {
            this.items = items.Select(item => new ItemEntry(item.ID, 1)).ToList();
        }
        public InventoryState(Dictionary<ItemData, int> itemDictionary) : this()
        {
            items = itemDictionary.Select(pair => new ItemEntry(pair.Key.ID, pair.Value)).ToList();
        }
        public void ToggleInventory()
        {
            if (shopUI != null && shopUI.IsOpen)
            {
                return;
            }

            if (inventoryManager != null)
            {
                bool isActive = !inventoryManager.gameObject.activeSelf;
                inventoryManager.gameObject.SetActive(isActive);
            }
        }
    }
}