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
        [Tooltip("무기 이미지의 자체 회전 오프셋 (기본 이미지가 45도 상단을 향하면 -45)")]
        public float spriteRotationOffset = 0f;
        [Tooltip("무기 장착 위치 오프셋 (손잡이 위치 조절용)")]
        public Vector3 visualPositionOffset = Vector3.zero;
        [Tooltip("무기 자체에 부착할 이펙트 프리팹 (예: 스태프의 파티클 시스템 등)")]
        public GameObject weaponEffectPrefab;

        [Tooltip("발사 시 발생할 이펙트 프리팹 (예: 머즐 플래시 등)")]
        public GameObject fireEffectPrefab;

        [Tooltip("차징(충전) 중 지속적으로 발생할 이펙트 프리팹 (예: 차징 기 축적 이펙트 등)")]
        public GameObject chargeEffectPrefab;

        [Header("Animation Settings")]
        [Tooltip("무기 전용 애니메이터 컨트롤러 (Idle, Fire 애니메이션 연동용)")]
        public RuntimeAnimatorController animatorController;

        [Header("Projectile Settings")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 10f;
        [Tooltip("투사체 이미지의 자체 회전 오프셋 (기본 이미지가 왼쪽을 향하면 180)")]
        public float projectileRotationOffset = 0f;
        public ExtraProjectileMode extraProjectileMode = ExtraProjectileMode.Spread;
        public float maxChargeTime = 1.0f;
        [Tooltip("차징 판정이 시작되기까지 누르고 있어야 하는 최소 시간(초)")]
        public float chargeThresholdTime = 0.15f;
        [Tooltip("이 무기가 차징 무기로 활성화되기 위해 필요한 유물 ID (비어 있으면 항상 차징 가능)")]
        public string requiredRelicId = "";
        [Tooltip("적 충돌 시 발생할 피격 이펙트 프리팹 (예: 독 속성 이펙트 등)")]
        public GameObject hitEffectPrefab;
        
        [Header("Prefab Settings")]
        public WeaponBase weaponPrefab;
        
        [Header("Archive System")]
        [Tooltip("이 무기의 투사체가 랜덤 아카이브 무기의 풀에 포함될지 여부")]
        public bool isArchivable = true;

        [System.NonSerialized] private float originalDamage;
        [System.NonSerialized] private float originalCooldown;
        [System.NonSerialized] private int originalBaseValue;
        [System.NonSerialized] private bool isStatsCached = false;

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheOriginalStats();
        }

        private void CacheOriginalStats()
        {
            if (isStatsCached) return;
            originalDamage = damage;
            originalCooldown = cooldown;
            originalBaseValue = baseValue;
            isStatsCached = true;
        }

        public override void ApplyRarityStats(Rarity targetRarity)
        {
            base.ApplyRarityStats(targetRarity);
            CacheOriginalStats();

            float damageMultiplier = 1f;
            float cooldownMultiplier = 1f;

            int minPrice = 135;
            int maxPrice = 165;

            switch (targetRarity)
            {
                case Rarity.Common:
                    damageMultiplier = 1.0f;
                    cooldownMultiplier = 1.0f;
                    minPrice = 135;
                    maxPrice = 165;
                    break;
                case Rarity.Uncommon:
                    damageMultiplier = 1.2f;
                    cooldownMultiplier = 0.9f;
                    minPrice = 270;
                    maxPrice = 330;
                    break;
                case Rarity.Rare:
                    damageMultiplier = 1.5f;
                    cooldownMultiplier = 0.8f;
                    minPrice = 540;
                    maxPrice = 660;
                    break;
                case Rarity.Epic:
                    damageMultiplier = 2.0f;
                    cooldownMultiplier = 0.7f;
                    minPrice = 1080;
                    maxPrice = 1320;
                    break;
                case Rarity.Legendary:
                    damageMultiplier = 3.0f;
                    cooldownMultiplier = 0.5f;
                    minPrice = 2250;
                    maxPrice = 2750;
                    break;
            }

            damage = originalDamage * damageMultiplier;
            cooldown = originalCooldown * cooldownMultiplier;

            int rawPrice = UnityEngine.Random.Range(minPrice, maxPrice + 1);
            baseValue = Mathf.RoundToInt(rawPrice / 10f) * 10;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            equipmentType = EquipmentType.Weapon;
        }
#endif
    }
}