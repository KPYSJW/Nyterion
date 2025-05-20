using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 근접 무기를 구현한 클래스입니다.
    /// WeaponBase를 상속받아 근접 공격에 특화된 기능을 제공합니다.
    /// 원형 충돌 검출을 사용하여 주변의 적에게 데미지를 입힙니다.
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


            // 4. 각 충돌체에 대해 데미지 처리
            foreach (RaycastHit2D hit in hits)
            {
                // 충돌한 오브젝트에서 IDamageable 컴포넌트 가져오기
                IDamageable target = hit.collider.GetComponent<IDamageable>();
                
                // IDamageable을 구현한 오브젝트인 경우에만 데미지 처리
                if (target != null)
                {
                    // 무기 데이터에 정의된 데미지만큼 데미지 적용
                    target.TakeDamage(weaponData.damage);
                    
                    // 여기에 추가 효과 (넉백, 상태 이상 등)를 구현할 수 있음
                    // 예: target.ApplyKnockback(transform.position, knockbackForce);
                }
            }

        }
        /// <summary>
        /// 공격 종료 시 호출되는 메서드입니다.
        /// 공격 애니메이션 종료 시나 공격 후 처리에 사용되며, 기본 색상으로 복원합니다.
        /// 공격 애니메이션이 완료된 후 또는 공격이 취소될 때 호출됩니다.
        /// </summary>

    }
}
