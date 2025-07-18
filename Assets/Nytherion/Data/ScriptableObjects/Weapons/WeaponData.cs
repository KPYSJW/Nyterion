using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.GamePlay.Combat;
using UnityEngine;
using Nytherion.Core.Enums;

namespace Nytherion.Data.ScriptableObjects.Weapons
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Data/Item/Weapon")]
    public class WeaponData : EquipmentData
    {
        [Header("Weapon Settings")]
        public string weaponName;
        public float damage;
        public float range;
        public float cooldown;
        public GameObject projectilePrefab;
        public WeaponBase weaponPrefab;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            equipmentType = EquipmentType.Weapon;
        }
#endif
    }
}