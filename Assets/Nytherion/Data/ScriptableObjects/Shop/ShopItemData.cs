using Nytherion.Data.ScriptableObjects.Items;
using System;

namespace Nytherion.Data.ScriptableObjects.Shop
{
    [Serializable]
    public class ShopItemData
    {
        public ItemData item;
        public int price;
        public int stock; 
        public bool isUnlimited;
    }
}
