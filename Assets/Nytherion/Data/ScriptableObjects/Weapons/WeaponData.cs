using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.Enums;

namespace Nytherion.Data.ScriptableObjects.Weapons
{
    public enum WeaponType
    {
        Melee,
        Ranged
    }

    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Data/Weapon")]
    public class WeaponData : ItemData
    {
        [Header("Weapon Settings")]
        public WeaponType weaponType;
        public string weaponName;
        public float damage;
        public float range;
        public float cooldown;
        public GameObject projectilePrefab;
        public bool isCursed;

        [Header("Gacha Settings")]
        public Rarity rarity;
    }


}
