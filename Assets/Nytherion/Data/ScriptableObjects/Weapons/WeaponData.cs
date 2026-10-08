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

        [Tooltip("인벤토리 외 슬롯과 툴팁의 대각선 배치입니다. 드래그 아이콘은 출발 슬롯의 표시를 따릅니다.")]
        public bool useDiagonalIcon = true;

        [Tooltip("대각선 배치의 -45도에 더할 각도입니다. 5를 입력하면 -40도로 표시합니다. 기본 배치에는 적용되지 않습니다.")]
        public float iconRotationOffset;

        [Min(0.1f)]
        [Tooltip("장착·상점 등 인벤토리 외 슬롯과 툴팁의 아이콘 크기 배율입니다. 드래그 아이콘은 출발 슬롯의 표시를 따릅니다.")]
        public float iconSlotScale = 1.5f;

        [Min(0.1f)]
        [Tooltip("인벤토리 전용 크기 보정입니다. 1은 현재 기본 크기, 0.75는 축소입니다. 확대 시 정수 픽셀 배율에 맞춰 표시됩니다.")]
        public float inventoryIconScale = 1f;

        [Header("Weapon Settings")]
        public float damage;
        public float range;
        public float cooldown;
        public WeaponType weaponType;

        /// <summary>
        /// 근거리 무기 구현이 다시 활성화될 때까지 런타임 등장 후보에서 제외합니다.
        /// 에셋과 저장 ID는 유지해 기존 참조가 손상되지 않도록 합니다.
        /// </summary>
        public bool IsRuntimeAvailable => weaponType != WeaponType.Melee;

        [Header("Visual Settings")]
        public Sprite weaponSprite;
        public Vector3 firePointOffset;
        [Tooltip("무기 이미지의 자체 회전 오프셋 (기본 이미지가 45도 상단을 향하면 -45)")]
        public float spriteRotationOffset = 0f;
        [Tooltip("무기 장착 위치 오프셋 (손잡이 위치 조절용)")]
        public Vector3 visualPositionOffset = Vector3.zero;
        [Min(0.01f)]
        [Tooltip("무기 외형의 균일 크기 배율")]
        public float visualScale = 1f;
        [Tooltip("플레이어 SpriteRenderer의 Sorting Order에 더할 무기 표시 순서 오프셋")]
        public int sortingOrderOffset = -1;
        [Tooltip("범용 원거리 무기의 착용 위치와 각도를 고정하고 캐릭터 방향에 따라 좌우 반전합니다.")]
        public bool useFixedFacingPose;

        [Header("Staff Recoil Settings")]
        [Tooltip("발사 시 Frenzy와 동일한 무기 반동을 적용합니다.")]
        public bool useStaffRecoil;

        [Tooltip("무기 자체에 부착할 이펙트 프리팹 (예: 스태프의 파티클 시스템 등)")]
        public GameObject weaponEffectPrefab;

        [Tooltip("발사 시 재생할 이펙트 프리팹입니다. 무기 데이터 Inspector의 '발사 이미지 설정'에서 스프라이트 이미지로 생성·연결할 수 있습니다.")]
        public GameObject fireEffectPrefab;

        [Tooltip("차징(충전) 중 지속적으로 발생할 이펙트 프리팹 (예: 차징 기 축적 이펙트 등)")]
        public GameObject chargeEffectPrefab;

        [Tooltip("차징 이펙트의 초당 Z축 회전 각도 (0: 회전 없음, 음수: 반대 방향)")]
        public float chargeEffectRotationSpeed = 0f;

        [Header("Animation Settings")]
        [Tooltip("무기 전용 애니메이터 컨트롤러 (Idle, Fire 애니메이션 연동용)")]
        public RuntimeAnimatorController animatorController;

        [Header("Projectile Settings")]
        public GameObject projectilePrefab;
        [Tooltip("발사된 투사체마다 순환할 애니메이터입니다. 비어 있으면 프리팹의 애니메이션을 사용합니다.")]
        public RuntimeAnimatorController[] projectileAnimationVariants;
        [Tooltip("투사체 이미지와 같은 순서로 연결할 시작 이펙트입니다. 원형 발사 시 이펙트를 한 번 재생한 뒤 대응하는 탄을 발사합니다.")]
        public GameObject[] projectileStartEffectVariants;
        public float projectileSpeed = 10f;
        [Tooltip("투사체 이미지의 자체 회전 오프셋 (기본 이미지가 왼쪽을 향하면 180)")]
        public float projectileRotationOffset = 0f;
        public ExtraProjectileMode extraProjectileMode = ExtraProjectileMode.Spread;
        public float maxChargeTime = 1.0f;
        [Tooltip("이 무기가 발사하는 투사체는 유도 유물이 없어도 기본 유도를 사용합니다.")]
        public bool hasHomingProjectiles;
        [Header("플레이어 주변 원형 발사")]
        [Tooltip("플레이어 주변 원의 서로 다른 지점을 무작위로 골라 투사체를 발사합니다.")]
        public bool usePlayerCircleSpawn;
        [Min(0.01f), Tooltip("플레이어 중심에서 발사 지점까지의 월드 거리")]
        public float playerCircleSpawnRadius = 0.65f;
        [Min(1), Tooltip("원 위에 균등하게 배치할 지점 수. 투사체가 더 많으면 해당 공격의 지점 수를 늘립니다.")]
        public int playerCircleSpawnPointCount = 12;
        [Header("유도 마법탄")]
        [Tooltip("목표 방향 기준으로 유도탄의 발사 각도를 배치합니다. 발사 수는 기본 발사 수와 투사체 증가 효과를 따릅니다.")]
        public bool useHomingLaunchAngles;
        [Tooltip("추가 탄이 생길 때 중앙부터 넓혀 사용할 각도 배치입니다. 기본 1발은 항상 0도로 출발합니다.")]
        public float[] homingLaunchAngles = { 30f, 15f, 0f, -15f, -30f };
        [Min(0f), Tooltip("초당 최대 회전 각도")]
        public float homingTurnSpeed = 180f;
        [Tooltip("유도 중에도 발사 속도를 유지하고 근거리에서는 회전 속도를 보정합니다.")]
        public bool useConstantHomingSpeed;
        [Min(0f), Tooltip("발사 시 살아 있는 적을 선택할 탐색 반경")]
        public float homingSearchRadius = 10f;
        [Min(0f), Tooltip("초기 방향을 유지한 뒤 유도를 시작할 시간")]
        public float homingLaunchDuration = 0.15f;
        [Min(0.01f), Tooltip("유도 마법탄의 수명(초)")]
        public float homingLifetime = 4f;
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
