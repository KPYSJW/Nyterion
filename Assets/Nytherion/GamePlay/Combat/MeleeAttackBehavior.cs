using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Gameplay.Combat
{
    /// <summary>
    /// 근접 공격 행동을 구현한 클래스입니다.
    /// IAttackBehavior 인터페이스를 구현하여 근접 공격 로직을 처리합니다.
    /// </summary>
    public class MeleeAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [Header("Attack Settings")]
        [Tooltip("근접 공격의 최대 사거리")]
        [SerializeField] private float attackRange = 1.5f;
        
        [Tooltip("공격 쿨다운 시간(초)")]
        [SerializeField] private float attackCoolDown = 1f;
        
        private float lastAttackTime = -999f;

        /// <summary>
        /// 현재 공격 쿨다운 상태를 가져옵니다.
        /// </summary>
        /// <value>0에서 1 사이의 값으로, 1이면 쿨다운이 완료된 상태입니다.</value>
        public float AttackCoolDown => Mathf.Clamp01((Time.time - lastAttackTime) / attackCoolDown);
        /// <summary>
        /// 대상이 공격 범위 내에 있는지 확인합니다.
        /// </summary>
        /// <param name="target">확인할 대상의 Transform</param>
        /// <returns>대상이 공격 범위 내에 있으면 true, 그렇지 않으면 false</returns>
        public bool IsInAttackRange(Transform target)
        {
            if (target == null) return false;
            return Vector2.Distance(transform.position, target.position) <= attackRange;
        }

        /// <summary>
        /// 대상에 대해 공격을 시도합니다.
        /// </summary>
        /// <param name="target">공격 대상의 Transform</param>
        /// <returns>공격이 성공적으로 수행되면 true, 그렇지 않으면 false</returns>
        public bool TryAttack(Transform target)
        {
            if (target == null) return false;
            
            bool canAttack = Time.time - lastAttackTime >= attackCoolDown && IsInAttackRange(target);
            
            if (canAttack)
            {
                // 공격 로직 수행
                lastAttackTime = Time.time;
                Debug.Log($"{gameObject.name}이(가) {target.name}을(를) 근접 공격했습니다!");
                
                // TODO: 실제 공격 로직 구현 (데미지 처리 등)
                // 예: target.GetComponent<IDamageable>()?.TakeDamage(damage);
            }
            
            return canAttack;
        }
    }
}
