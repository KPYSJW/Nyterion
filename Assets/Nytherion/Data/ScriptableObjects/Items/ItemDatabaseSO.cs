using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Items
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Nytherion/Items/Item Database")]
    public class ItemDatabaseSO : ScriptableObject
    {
        public List<ItemData> allItems;
    }
}