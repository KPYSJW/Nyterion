using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.Core
{
    public static class ItemDatabase
    {
        private static Dictionary<string, ItemData> itemTable;
        private static bool isInitialized = false;

        public static void Initialize()
        {
            if (isInitialized) return;

            itemTable = new Dictionary<string, ItemData>();

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/Nytherion/Data/ScriptableObjects/Items" });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && !string.IsNullOrEmpty(item.ID) && !itemTable.ContainsKey(item.ID))
                {
                    itemTable[item.ID] = item;
                }
            }
#endif

            if (Application.isPlaying && itemTable.Count == 0)
            {
                var allItems = Resources.LoadAll<ItemData>("Items");
                foreach (var item in allItems)
                {
                    if (item != null && !string.IsNullOrEmpty(item.ID) && !itemTable.ContainsKey(item.ID))
                    {
                        itemTable[item.ID] = item;
                    }
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