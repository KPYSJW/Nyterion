using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Items;
using System;

namespace Nytherion.UI.Inventory
{
    [Serializable]
    public class InventoryState
    {
        [SerializeField] private List<string> itemIds = new List<string>();

        public IReadOnlyList<string> ItemIds => itemIds;

        public InventoryState(IEnumerable<ItemData> items)
        {
            itemIds = items.Select(item => item.ID).ToList();
        }

        // JSON 직렬화를 위한 빈 생성자
        public InventoryState() { }
    }
}