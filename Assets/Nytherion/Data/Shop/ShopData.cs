using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.Shop
{
    [CreateAssetMenu(fileName = "NewShopData", menuName = "Data/Shop")]
    public class ShopData : ScriptableObject
    {
       public string shopName;
       public List<ShopItemData> itemsForSale;
    }
}
