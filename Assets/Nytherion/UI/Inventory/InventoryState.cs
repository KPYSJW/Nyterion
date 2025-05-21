using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Inventory
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

        [SerializeField] private List<ItemEntry> items = new List<ItemEntry>();

        public IReadOnlyList<ItemEntry> Items => items;
        
        // 이전 버전과의 호환성을 위한 속성
        [Obsolete("Use Items property instead")]
        public IReadOnlyList<string> ItemIds => items.Select(entry => entry.ItemId).ToList();

        public InventoryState() { }

        // 기존 코드와의 호환성을 위한 생성자
        public InventoryState(IEnumerable<ItemData> items) : this()
        {
            this.items = items.Select(item => new ItemEntry(item.ID, 1)).ToList();
        }

        // 새로운 생성자
        public InventoryState(Dictionary<ItemData, int> itemDictionary) : this()
        {
            items = itemDictionary.Select(pair => new ItemEntry(pair.Key.ID, pair.Value)).ToList();
        }
    }
}