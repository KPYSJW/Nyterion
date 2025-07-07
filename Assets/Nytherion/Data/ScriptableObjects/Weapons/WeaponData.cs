using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Weapons
{
    public enum WeaponType
    {
        Melee,
        Ranged
    }

    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Data/Item/Weapon")]
    public class WeaponData : EquipmentData
    {
        [Header("Weapon Settings")]
        public WeaponType weaponType;
        public string weaponName;
        public float damage;
        public float range;
        public float cooldown;
        public GameObject projectilePrefab;
        public WeaponBase weaponPrefab;
    }
}