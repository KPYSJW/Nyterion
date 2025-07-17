using UnityEngine;
using Nytherion.Core.Enums;

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

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            equipmentType = EquipmentType.Armor;
        }
#endif
    }
}