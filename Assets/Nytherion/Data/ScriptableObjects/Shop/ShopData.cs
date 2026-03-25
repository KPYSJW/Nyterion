using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Shop
{
    [CreateAssetMenu(fileName = "NewShopData", menuName = "Data/Shop")]
    public class ShopData : ScriptableObject
    {
        public string shopName;
        public List<ShopItemData> itemsForSale;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (itemsForSale == null) return;

            HashSet<string> useIds = new HashSet<string>();
            bool isDirty = false;

            foreach (var shopItem in itemsForSale)
            {
                if (string.IsNullOrEmpty(shopItem.shopItemId) || useIds.Contains(shopItem.shopItemId))
                {
                    shopItem.shopItemId = System.Guid.NewGuid().ToString();
                    isDirty = true;
                }
                useIds.Add(shopItem.shopItemId);
            }
            if (isDirty)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}