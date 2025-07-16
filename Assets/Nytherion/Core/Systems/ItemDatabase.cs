using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.Core.Systems
{
    public static class ItemDatabase
    {
        private static Dictionary<string, ItemData> itemTable;
        private static bool isInitialized = false;

        public static void Initialize(ItemDatabaseSO databaseSO)
        {
            if (isInitialized) return;

            itemTable = new Dictionary<string, ItemData>();

            if (databaseSO == null)
            {
                Debug.LogError("[ItemDatabase] 전달된 ItemDatabaseSO 에셋이 null입니다! GameInitializer를 확인하세요.");
                return;
            }

            foreach (var item in databaseSO.allItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.ID))
                {
                    if (itemTable.ContainsKey(item.ID))
                    {
                        Debug.LogWarning($"[ItemDatabase] 중복된 아이템 ID를 감지했습니다: {item.ID} (아이템: {item.name})");
                        continue;
                    }
                    itemTable[item.ID] = item;
                }
            }

            isInitialized = true;
        }
        public static ItemData GetItemByID(string id)
        {
            if (!isInitialized || string.IsNullOrEmpty(id)) return null;
            itemTable.TryGetValue(id, out ItemData item);
            return item;
        }

        public static IEnumerable<ItemData> GetAllItems()
        {
            if (!isInitialized) return Enumerable.Empty<ItemData>();
            return itemTable.Values;
        }
    }
}