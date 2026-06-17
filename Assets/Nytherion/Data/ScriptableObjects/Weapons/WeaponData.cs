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
        [Tooltip("적 충돌 시 발생할 피격 이펙트 프리팹 (예: 독 속성 이펙트 등)")]
        public GameObject hitEffectPrefab;
        
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