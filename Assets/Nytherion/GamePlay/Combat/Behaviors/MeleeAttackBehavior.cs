using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Behaviors
{
    public class MeleeAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [Header("Attack Settings")]
        [Tooltip("근접 공격의 최대 사거리")]
        [SerializeField] private float attackRange = 1.5f;

        [Tooltip("공격 쿨다운 시간(초)")]
        [SerializeField] private float attackCoolDown = 1f;
        [SerializeField] private float fallbackDamage = 10;
        private float lastAttackTime = -999f;
        private EnemyBase enemyBase;

        public float AttackCoolDown => Mathf.Clamp01((Time.time - lastAttackTime) / attackCoolDown);

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
        }

        public bool IsInAttackRange(Transform target)
        {
            if (target == null) return false;
            return (transform.position - target.position).sqrMagnitude <= attackRange * attackRange;
        }


        public bool TryAttack(Transform target)
        {
            if (target == null) return false;

            bool canAttack = Time.time - lastAttackTime >= attackCoolDown && IsInAttackRange(target);

            if (!canAttack) return false;

            lastAttackTime = Time.time;
            ApplyDamage(target);


            return canAttack;
        }

        private void ApplyDamage(Transform target)
        {
            float damage = GetDamageValue();

            if (target.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damage);
                return;
            }

            if (target.TryGetComponent<PlayerHealth>(out var playerHealth))
            {
                playerHealth.TakeDamage(damage);
                return;
            }
        }

        private float GetDamageValue()
        {
            if (enemyBase != null && enemyBase.enemyData != null)
            {
                return enemyBase.enemyData.damageAmount;
            }

            return fallbackDamage;
        }
    }
}
