using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Gameplay.Combat
{
    public class MeleeAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        public float attackRange = 1.5f;
        public float attackCoolDown = 1f;
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
                Debug.Log("MeleeAttack");
                lastAttackTime = Time.time;
            }
        }
    }
}
