using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Game.Combat
{
    public class RangedAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        public float attackRange = 5f;
        public float attackCoolDown = 2f;
        public GameObject projectilePrefab;
        public Transform firePoint;
        private float lastAttackTime = -999f;

        public float AttackCoolDown => attackCoolDown;
        public bool IsInAttackRange(Transform target)
        {
            return Vector2.Distance(transform.position, target.position) <= attackRange;
        }

        public void TryAttack(Transform target)
        {
            if (Time.time - lastAttackTime >= attackCoolDown && IsInAttackRange(target))
            {
                Debug.Log("RangedAttack");
                Vector2 direction = (target.position - transform.position).normalized;
                GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
                projectile.GetComponent<Rigidbody2D>().velocity = direction * 8f;
                lastAttackTime = Time.time;
            }
        }
    }
}
