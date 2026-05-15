using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.GamePlay.Combat;
using UnityEngine;
using Nytherion.Core.Enums;
using UnityEngine.Serialization;

 public enum WeaponType
        {
            Ranged,
            Melee
        };

namespace Nytherion.Data.ScriptableObjects.Weapons
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Data/Item/Weapon")]

    
    public class WeaponData : EquipmentData
    {
        public string weaponName => itemName;

        [Header("Weapon Settings")]
        public float damage;
        public float range;
        public float cooldown;
        public WeaponType weaponType;

        [Header("Visual Settings")]
        public Sprite weaponSprite;
        public Vector3 firePointOffset;

        [Header("Projectile Settings")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 10f;
        public ExtraProjectileMode extraProjectileMode = ExtraProjectileMode.Spread;
        public float maxChargeTime = 1.0f;
        
        [Header("Prefab Settings")]
        public WeaponBase weaponPrefab;
        
        [Header("Archive System")]
        [Tooltip("이 무기의 투사체가 랜덤 아카이브 무기의 풀에 포함될지 여부")]
        public bool isArchivable = true;
       

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            equipmentType = EquipmentType.Weapon;
        }
#endif
    }
}