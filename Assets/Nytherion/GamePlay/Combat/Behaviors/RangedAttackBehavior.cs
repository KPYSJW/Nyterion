using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Behaviors
{
    public class RangedAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [Header("Attack Settings")]
        [SerializeField] private float attackRange = 5f;
        [SerializeField] private float attackCoolDown = 2f;
        [SerializeField] private float fallbackDamage = 8f;

        [Header("Projectile Visual")]
        [SerializeField] private bool useProjectileVisual = true;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] public Transform firePoint;

        private float lastAttackTime = -999f;
        private const float ProjectileSpeed = 8f;
        private EnemyBase enemyBase;

        public float AttackCoolDown => Mathf.Clamp01((Time.time - lastAttackTime) / attackCoolDown);

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
        }

        public bool IsInAttackRange(Transform target)
        {
            if (target == null) return false;
            return Vector2.Distance(transform.position, target.position) <= attackRange;
        }

        public bool TryAttack(Transform target)
        {
            if (target == null) return false;

            bool canAttack = Time.time - lastAttackTime >= attackCoolDown && IsInAttackRange(target);
            if (!canAttack) return false;

            lastAttackTime = Time.time;
            ApplyDamage(target);
            SpawnProjectileVisual(target);

            return true;
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
            }
        }

        private void SpawnProjectileVisual(Transform target)
        {
            if (!useProjectileVisual) return;
            if (projectilePrefab == null || firePoint == null) return;

            Vector2 direction = (target.position - firePoint.position).normalized;
            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

            if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.velocity = direction * ProjectileSpeed;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
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
