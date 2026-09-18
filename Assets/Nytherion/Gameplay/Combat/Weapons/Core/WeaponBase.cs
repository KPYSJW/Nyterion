using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public abstract class WeaponBase : MonoBehaviour
    {
        
        [SerializeField]public WeaponData weaponData;
        
        [Tooltip("무기가 자체적으로 회전 및 스케일 제어를 제어할지 여부")]
        public virtual bool OverrideRotation => false;

        /// <summary>공격 버튼을 누르고 있을 때 쿨다운마다 자동으로 재공격할 수 있는지 여부입니다.</summary>
        public virtual bool AllowAutoFire => true;

        /// <summary>차징 무기가 공격 불가 중 들어온 누름 상태를 유지했다가 가능해지는 순간 다시 시작할지 여부입니다.</summary>
        public virtual bool AllowHeldAttackRetry => false;
        
        [Tooltip("마지막 공격 시간 (Time.time 기준)")]
        protected float lastAttackTime;

        public float damageMultiplier = 1.0f;
        private float genericChargeDamageMultiplier = 1.0f;

        protected float EffectiveDamageMultiplier => damageMultiplier * genericChargeDamageMultiplier;
        public float CurrentDamageMultiplier => EffectiveDamageMultiplier;

        protected PlayerManager playerManager;
        [SerializeField] protected Animator animator;
        private WeaponCrossbowRecoil staffRecoil;

        protected virtual void Awake()
        {
            playerManager = GetComponentInParent<PlayerManager>();
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (GetComponent<WeaponSortingOrderSync>() == null)
            {
                gameObject.AddComponent<WeaponSortingOrderSync>();
            }

            ConfigureStaffRecoil(weaponData);
        }

        protected void PlayFireAnimation()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger("Fire");
            }
        }

        public virtual void Initialize(WeaponData data)
        {
            weaponData = data;
            lastAttackTime = -data.cooldown;
            genericChargeDamageMultiplier = 1f;
            ConfigureStaffRecoil(data);

            if (data.weaponSprite != null)
            {
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                if (sr == null)
                {
                    sr = GetComponentInChildren<SpriteRenderer>();
                }

                if (sr != null)
                {
                    sr.sprite = data.weaponSprite;
                }
            }

            // 무기 고유 이펙트 프리팹 생성 및 부착
            if (data.weaponEffectPrefab != null)
            {
                GameObject effectObj = Instantiate(data.weaponEffectPrefab, this.transform);
                effectObj.transform.localPosition = data.weaponEffectPrefab.transform.localPosition;
                effectObj.transform.localRotation = data.weaponEffectPrefab.transform.localRotation;
                effectObj.transform.localScale = data.weaponEffectPrefab.transform.localScale;
            }
        }

        public virtual bool CanAttack()
        {
            if (weaponData == null)
            {
                return true;
            }
            return Time.time - lastAttackTime >= weaponData.cooldown;
        }
        public virtual void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
        }
        public abstract void AttackEnd();

        protected bool PlayStaffRecoil(float strength = 1f)
        {
            if (staffRecoil == null || !staffRecoil.enabled)
            {
                return false;
            }

            staffRecoil.PlayLocalBack(strength);
            return true;
        }

        private void ConfigureStaffRecoil(WeaponData data)
        {
            if (data == null || !data.useStaffRecoil)
            {
                staffRecoil = null;
                return;
            }

            if (staffRecoil == null)
            {
                staffRecoil = GetComponent<WeaponCrossbowRecoil>();
            }

            if (staffRecoil == null)
            {
                staffRecoil = gameObject.AddComponent<WeaponCrossbowRecoil>();
            }

            staffRecoil.enabled = true;
        }

        public virtual void AttackWithGenericCharge(Vector2 direction, Vector3 targetPosition, float chargePercent)
        {
            genericChargeDamageMultiplier = Mathf.Lerp(1f, 2f, Mathf.Clamp01(chargePercent));
            Attack(direction, targetPosition);
        }

        public void ResetGenericChargeMultiplier()
        {
            genericChargeDamageMultiplier = 1f;
        }

        public virtual List<EquipmentTrait> GetTraits()
        {
            if (weaponData != null && weaponData.traits != null)
            {
                return weaponData.traits;
            }
            return new List<EquipmentTrait>();
        }

        public void ApplyStatusEffects(IDamageable target)
        {
            if (target is MonoBehaviour targetMono &&
                targetMono.TryGetComponent<StatusEffectManager>(out StatusEffectManager effectManager))
            {
                List<EquipmentTrait> activeTraits = GetTraits();
                for (int i = 0; i < activeTraits.Count; i++)
                {
                    switch (activeTraits[i])
                    {
                        case EquipmentTrait.Fire:
                            float burnDamage = weaponData != null ? Mathf.Max(1f, weaponData.damage * EffectiveDamageMultiplier * 0.2f) : 2f;
                            effectManager.ApplyEffect(new FireEffect(burnDamage, 5f));
                            break;
                        case EquipmentTrait.Curse:
                            effectManager.ApplyEffect(new CurseEffect(1.1f, 5f));
                            break;
                        case EquipmentTrait.Ice:
                            effectManager.ApplyEffect(new IceEffect(5f));
                            break;
                        case EquipmentTrait.Lightning:
                            effectManager.ApplyEffect(new LightningEffect(5f));
                            break;
                        case EquipmentTrait.Holy:
                            effectManager.ApplyEffect(new HolyEffect(5f));
                            break;
                        case EquipmentTrait.Demonic:
                            effectManager.ApplyEffect(new DemonicEffect(5f));
                            break;
                        case EquipmentTrait.Poison:
                            effectManager.ApplyEffect(new PoisonEffect(3f, 5f));
                            break;
                    }
                }
            }
        }
    }
}
