using UnityEngine;
using Nytherion.Core.Enums;

namespace Nytherion.Data.ScriptableObjects.Items
{
    public abstract class EquipmentData : ItemData
    {
        [Header("Equipment Settings")]
        public EquipmentType equipmentType;
        public Rarity rarity;
        public bool isCursed;

        protected void OnEnable()
        {
            isStackable = false;
            maxStack = 1;
        }
    }
}