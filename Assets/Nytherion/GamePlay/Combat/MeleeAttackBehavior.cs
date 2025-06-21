using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class MeleeAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [Header("Attack Settings")]
        [Tooltip("근접 공격의 최대 사거리")]
        [SerializeField] private float attackRange = 1.5f;
        
        [Tooltip("공격 쿨다운 시간(초)")]
        [SerializeField] private float attackCoolDown = 1f;
        
        private float lastAttackTime = -999f;

        public float AttackCoolDown => Mathf.Clamp01((Time.time - lastAttackTime) / attackCoolDown);
        public bool IsInAttackRange(Transform target)
        {
            if (target == null) return false;
            return Vector2.Distance(transform.position, target.position) <= attackRange;
        }

       
        public bool TryAttack(Transform target)
        {
            if (target == null) return false;
            
            bool canAttack = Time.time - lastAttackTime >= attackCoolDown && IsInAttackRange(target);
            
            if (canAttack)
            {
                lastAttackTime = Time.time;
                
                // TODO: 실제 공격 로직 구현 (데미지 처리 등)
            }
            
            return canAttack;
        }
    }
}
