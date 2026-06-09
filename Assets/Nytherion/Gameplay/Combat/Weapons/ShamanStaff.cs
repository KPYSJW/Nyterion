using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Data.ScriptableObjects.Weapons;
using System.Linq;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 주술사의 지팡이(Shaman Staff) 클래스.
    /// 기본적으로 ManaBall을 발사하나, '심연의 눈(Eye of Abyss)' 유물이 있을 경우 AbyssalFlame을 발사함.
    /// </summary>
    public class ShamanStaff : RangedWeapon
    {
        [Header("Shaman Staff Projectiles")]
        [SerializeField] private GameObject defaultProjectilePrefab; // ManaBall
        [SerializeField] private GameObject abyssalFlamePrefab; // AbyssalFlame

        public override void Initialize(WeaponData data)
        {
            base.Initialize(data);

            if (data != null && data.projectilePrefab != null)
            {
                defaultProjectilePrefab = data.projectilePrefab;
            }

            CheckAbyssRelicAndSwapProjectile();
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            // 유물 체크 및 투사체 스왑
            CheckAbyssRelicAndSwapProjectile();

            FireProjectiles(direction, 1);
            
            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
        }

        private bool HasAbyssRelic()
        {
            if (playerManager == null) return false;

            PlayerRelicManager relicManager = playerManager.GetComponent<PlayerRelicManager>();
            if (relicManager == null) return false;

            // "Eye of Abyss" 또는 "심연의 눈" 유물이 있는지 체크
            bool hasAbyssEye = relicManager.GetCurrentRelics().Any(relic => 
                relic != null && (relic.relicName == "Eye of Abyss" || relic.koreanName == "심연의 눈")
            );

            return hasAbyssEye;
        }

        private void CheckAbyssRelicAndSwapProjectile()
        {
            if (HasAbyssRelic())
            {
                if (abyssalFlamePrefab != null)
                {
                    currentProjectilePrefab = abyssalFlamePrefab;
                    projectilePoolTag = abyssalFlamePrefab.name;
                }
            }
            else
            {
                if (defaultProjectilePrefab != null)
                {
                    currentProjectilePrefab = defaultProjectilePrefab;
                    projectilePoolTag = defaultProjectilePrefab.name;
                }
            }
        }

        public override List<EquipmentTrait> GetTraits()
        {
            List<EquipmentTrait> baseTraits = base.GetTraits();
            if (HasAbyssRelic())
            {
                List<EquipmentTrait> modifiedTraits = new List<EquipmentTrait>(baseTraits);
                if (!modifiedTraits.Contains(EquipmentTrait.Curse))
                {
                    modifiedTraits.Add(EquipmentTrait.Curse);
                }
                if (!modifiedTraits.Contains(EquipmentTrait.Fire))
                {
                    modifiedTraits.Add(EquipmentTrait.Fire);
                }
                return modifiedTraits;
            }
            return baseTraits;
        }
    }
}
