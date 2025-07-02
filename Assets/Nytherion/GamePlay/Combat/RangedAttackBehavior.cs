using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
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

        public float AttackCoolDown => Mathf.Clamp01((Time.time - lastAttackTime) / attackCoolDown);
        
        public bool IsInAttackRange(Transform target)
        {
            if (target == null) return false;
            return Vector2.Distance(transform.position, target.position) <= attackRange;
        }

        
        public bool TryAttack(Transform target)
        {
            if (target == null || projectilePrefab == null || firePoint == null)
            {
                return false;
            }
            
            bool canAttack = Time.time - lastAttackTime >= attackCoolDown && IsInAttackRange(target);
            
            if (canAttack)
            {
                try
                {
                    Vector2 direction = (target.position - firePoint.position).normalized;
                    
                    GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
                    if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
                    {
                        rb.velocity = direction * ProjectileSpeed;
                        
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                    }
                    
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
