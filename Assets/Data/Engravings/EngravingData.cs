using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data
{
    [CreateAssetMenu(fileName = "NewEngravingData", menuName = "Data/Engraving")]
    public class EngravingData : ScriptableObject
    {
        public string engravingName;
        [TextArea]
        public string description;
        public Sprite icon;

        public enum StatType
        {
            MaxHealth,
            MoveSpeed,
            AttackPower,
            CooldownReduction,
            CriticalChance
        }
        [System.Serializable]
        public class StatModifier
        {
            public StatType statType;
            public float modifierValue;
        }

        public StatModifier[] statModifiers;

    }
}