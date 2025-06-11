using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using System;

namespace Nytherion.Data.Shop
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
