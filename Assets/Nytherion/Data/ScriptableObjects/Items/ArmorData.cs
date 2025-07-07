using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Items
{
    public enum ArmorType
    {
        Helmet,
        Armor,
        Boots,
        Accessory
    }

    [CreateAssetMenu(fileName = "NewArmorData", menuName = "Data/Item/Armor")]
    public class ArmorData : EquipmentData
    {
        [Header("Armor Settings")]
        public ArmorType armorType;
        public float defense;
    }
}