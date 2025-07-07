using UnityEngine;
using Nytherion.Data.Enums;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.Data.ScriptableObjects.Items
{
    public abstract class EquipmentData : ItemData
    {
        [Header("Equipment Settings")]
        public Rarity rarity;
        public bool isCursed;

        protected virtual void OnEnable()
        {
            isStackable = false;
            maxStack = 1;
        }
    }
}