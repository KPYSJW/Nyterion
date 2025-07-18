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
            foreach (var shopItem in itemsForSale)
            {
                shopItem.EnsureId();
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}