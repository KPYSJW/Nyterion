using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat
{
    public abstract class MeleeWeapon : WeaponBase
    {
        [Header("Melee Settings")]
        [Tooltip("무기의 시각적 표현을 담당하는 스프라이트 렌더러")]

        public Collider2D col;



        public void Collider(bool value)
        {
            col.enabled = value;
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
                    
                    // 여기에 추가 효과 (넉백, 상태 이상 등)를 구현할 수 있음
                }
            }

        }
    }
}
