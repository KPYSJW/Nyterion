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
    public class MeleeWeapon : WeaponBase
    {
        [Header("Melee Settings")]
        [Tooltip("무기의 시각적 표현을 담당하는 스프라이트 렌더러")]
        public SpriteRenderer sprite;
        
        /// <summary>공격 시 표시될 색상 (빨간색)</summary>
        private readonly Color _attackColor = new Color(1, 0, 0);
        
        /// <summary>기본 색상 (흰색, 완전 불투명)</summary>
        private readonly Color _defaultColor = new Color(1, 1, 1, 1);
        /// <summary>
        /// 지정된 방향으로 근접 공격을 수행합니다.
        /// 공격 범위 내의 모든 대상에게 데미지를 입힙니다.
        /// </summary>
        /// <param name="direction">공격 방향 (현재는 사용되지 않음)</param>
        public override void Attack(Vector2 direction)
        {
            // 1. 공격 애니메이션 시작 (빨간색으로 변경)
            sprite.color = _attackColor;

            // 2. 공격 쿨다운 확인
            if (!CanAttack()) 
            {
                // 쿨다운 중이면 색상만 복원하고 종료
                sprite.color = _defaultColor;
                return;
            }

            // 3. 원형 캐스트를 사용하여 공격 범위 내의 모든 충돌체 검출
            //    CircleCastAll은 지정된 반경 내의 모든 충돌체를 감지합니다.
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                transform.position,  // 검출 중심점 (무기 위치)
                weaponData.range,    // 검출 반경 (무기 데이터에서 가져옴)
                Vector2.zero);      // 방향 (0,0은 모든 방향으로 검출)


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


            // 5. 마지막 공격 시간을 현재 시간으로 갱신 (쿨다운 시작)
            lastAttackTime = Time.time;
        }

        /// <summary>
        /// 공격 종료 시 호출되는 메서드입니다.
        /// 공격 애니메이션 종료 시나 공격 후 처리에 사용되며, 기본 색상으로 복원합니다.
        /// 공격 애니메이션이 완료된 후 또는 공격이 취소될 때 호출됩니다.
        /// </summary>
        public override void AttackEnd()
        {
            // 1. 무기 색상을 기본 색상(흰색)으로 복원
            sprite.color = _defaultColor;
            
            // 2. 추가적인 공격 종료 처리 (필요한 경우)
            // 예: 공격 이펙트 비활성화, 사운드 정지 등
        }
    }
}
