using UnityEngine;
using Nytherion.Core.Interfaces;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Combat
{
    public abstract class MeleeWeapon : WeaponBase
    {
        [Header("Melee Settings")]
        [Tooltip("무기의 시각적 표현을 담당하는 스프라이트 렌더러")]

        public Collider2D col;
        private readonly HashSet<IDamageable> hitTargets = new();
        public void Collider(bool value)
        {
            col.enabled = value;
        }
        public void EnableHitbox()
        {
            if (col != null)
            {
                col.enabled = true;
            }
        }

        public void DisableHitbox()
        {
            if (col != null)
            {
                col.enabled = false;
            }
            ResetHitTargets();
        }

         public void ResetHitTargets()
        {
            hitTargets.Clear();
        }
        public void RayCast()
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
               transform.position,
               weaponData.range,
               Vector2.zero);

            foreach (RaycastHit2D hit in hits)
            {
                IDamageable target = hit.collider.GetComponent<IDamageable>();
                
                if (target != null)
                {
                    target.TakeDamage(weaponData.damage);
                    ApplyStatusEffects(target);
                    if (weaponData != null)
                    {
                        WeaponEffectHelper.PlayHitEffect(weaponData.hitEffectPrefab, hit.collider.transform.position);
                    }
                }
            }

        }

        public virtual void Start()
        {
            DisableHitbox();
            WeaponAniRelay weaponAniRelay=GetComponentInParent<WeaponAniRelay>();
            //if(weaponAniRelay!=null)Debug.Log("가나다라마사바");
            weaponAniRelay.currentWeapon=this;
        }

         private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.CompareTag("Enemy")) return;
            
            IDamageable target;
            if (!collision.TryGetComponent<IDamageable>(out target)) return;
            
            if (hitTargets.Contains(target)) return;
            hitTargets.Add(target);
            target.TakeDamage(weaponData.damage);
            ApplyStatusEffects(target);

            if (weaponData != null)
            {
                WeaponEffectHelper.PlayHitEffect(weaponData.hitEffectPrefab, collision.transform.position);
            }
        }
    }
}
