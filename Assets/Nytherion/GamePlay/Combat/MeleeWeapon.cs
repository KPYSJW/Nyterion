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
    public class MeleeWeapon : WeaponBase
    {
        [Header("Melee Settings")]
        [Tooltip("무기의 시각적 표현을 담당하는 스프라이트 렌더러")]
        public SpriteRenderer sprite;
        
        private readonly Color _attackColor = new Color(1, 0, 0); // 빨간색
        private readonly Color _defaultColor = new Color(1, 1, 1, 1); // 흰색, 완전 불투명
        /// <summary>
        /// 지정된 방향으로 근접 공격을 수행합니다.
        /// 공격 범위 내의 모든 대상에게 데미지를 입힙니다.
        /// </summary>
        /// <param name="direction">공격 방향 (현재는 사용되지 않음)</param>
        public override void Attack(Vector2 direction)
        {
            // 공격 애니메이션 시작 (빨간색으로 변경)
            sprite.color = _attackColor;

            // 공격 쿨다운 확인
            if (!CanAttack()) 
            {
                sprite.color = _defaultColor;
                return;
            }

            // 원형 캐스트를 사용하여 공격 범위 내의 모든 충돌체 검출
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

            // 마지막 공격 시간 갱신
            lastAttackTime = Time.time;
        }

        /// <summary>
        /// 공격 종료 시 호출되는 메서드입니다.
        /// 공격 애니메이션 종료 시나 공격 후 처리에 사용되며, 기본 색상으로 복원합니다.
        /// </summary>
        public override void AttackEnd()
        {
            // 기본 색상(흰색)으로 복원
            sprite.color = _defaultColor;
        }
    }
}
