using Nytherion.Data.ScriptableObjects.Items;
using System;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Shop
{
    [Serializable]
    public class ShopItemData
    {
        [HideInInspector]
        public string shopItemId;

        public ItemData item;
        public int price;
        public int stock;
        public bool isUnlimited;

#if UNITY_EDITOR
        public void EnsureId()
        {
            if (string.IsNullOrEmpty(shopItemId))
            {
                shopItemId = Guid.NewGuid().ToString();
            }
        }
#endif
    }
}