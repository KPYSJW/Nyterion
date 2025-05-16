using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Gameplay.Combat
{
    /// <summary>
    /// 원거리 공격 행동을 구현한 클래스입니다.
    /// IAttackBehavior 인터페이스를 구현하여 발사체 기반의 원거리 공격 로직을 처리합니다.
    /// </summary>
    public class RangedAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [Header("Attack Settings")]
        [Tooltip("원거리 공격의 최대 사거리")]
        [SerializeField] private float attackRange = 5f;
        
        [Tooltip("공격 쿨다운 시간(초)")]
        [SerializeField] private float attackCoolDown = 2f;
        
        [Header("Projectile Settings")]
        [Tooltip("발사체 프리팹")]
        [SerializeField] private GameObject projectilePrefab;
        
        [Tooltip("발사 지점")]
        [SerializeField] private Transform firePoint;
        
        private float lastAttackTime = -999f;
        private const float ProjectileSpeed = 8f;

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
        /// 대상에 대해 원거리 공격을 시도합니다.
        /// </summary>
        /// <param name="target">공격 대상의 Transform</param>
        /// <returns>공격이 성공적으로 수행되면 true, 그렇지 않으면 false</returns>
        public bool TryAttack(Transform target)
        {
            if (target == null || projectilePrefab == null || firePoint == null)
            {
                Debug.LogWarning("원거리 공격을 수행할 수 없습니다. 필요한 컴포넌트를 확인하세요.");
                return false;
            }
            
            bool canAttack = Time.time - lastAttackTime >= attackCoolDown && IsInAttackRange(target);
            
            if (canAttack)
            {
                try
                {
                    // 발사 방향 계산
                    Vector2 direction = (target.position - firePoint.position).normalized;
                    
                    // 발사체 생성 및 속도 설정
                    GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
                    if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
                    {
                        rb.velocity = direction * ProjectileSpeed;
                        
                        // 발사체가 타겟을 향하도록 회전 (옵션)
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                    }
                    
                    // 공격 성공 로그
                    Debug.Log($"{gameObject.name}이(가) {target.name}을(를) 향해 원거리 공격을 발사했습니다!");
                    
                    // 마지막 공격 시간 갱신
                    lastAttackTime = Time.time;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"발사체 생성 중 오류 발생: {ex.Message}");
                    return false;
                }
            }
            
            return canAttack;
        }
    }
}
