using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 근접 무기를 구현한 클래스입니다.
    /// WeaponBase를 상속받아 근접 공격에 특화된 기능을 제공합니다.
    /// </summary>
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

            // 각 충돌체에 대해 데미지 처리
            foreach (RaycastHit2D hit in hits)
            {
                var target = hit.collider.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(weaponData.damage);
                }
            }

        }
        /// <summary>
        /// 공격 종료 시 호출되는 메서드입니다.
        /// 공격 애니메이션 종료 시나 공격 후 처리에 사용되며, 기본 색상으로 복원합니다.
        /// </summary>


    }
}
