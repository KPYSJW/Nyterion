using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Data;


namespace Nytherion.Data.ScriptableObjects.Items
{
    public abstract class EquipmentData : ItemData
    {
        [Header("Equipment Settings")]
        public EquipmentType equipmentType;
        public Rarity rarity;
        public bool isCursed;

        [Header("Stat Modifiers")]
        public List<StatModifier> statModifiers;

        protected void OnEnable()
        {
            isStackable = false;
            maxStack = 1;
        }
    }
}