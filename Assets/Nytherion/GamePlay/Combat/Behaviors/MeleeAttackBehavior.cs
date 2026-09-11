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
        [SerializeField] private MeleeAttackCollider meleeAttackCollider;
        [Tooltip("지정하면 숫자 거리 대신 이 원형 콜라이더와 대상 콜라이더의 실제 범위로 공격 시작을 판정합니다.")]
        [SerializeField] private CircleCollider2D attackRangeCollider;
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

            if (attackRangeCollider != null)
            {
                Vector2 rangeCenter = attackRangeCollider.transform.TransformPoint(
                    attackRangeCollider.offset);
                Vector3 lossyScale = attackRangeCollider.transform.lossyScale;
                float rangeScale = Mathf.Max(
                    Mathf.Abs(lossyScale.x),
                    Mathf.Abs(lossyScale.y));
                float rangeRadius = attackRangeCollider.radius * rangeScale;

                Collider2D targetCollider = target.GetComponent<Collider2D>();
                if (targetCollider == null)
                {
                    targetCollider = target.GetComponentInChildren<Collider2D>();
                }

                Vector2 targetPoint = targetCollider != null
                    ? targetCollider.ClosestPoint(rangeCenter)
                    : (Vector2)target.position;

                return (targetPoint - rangeCenter).sqrMagnitude <=
                       rangeRadius * rangeRadius;
            }

            return (transform.position - target.position).sqrMagnitude <= attackRange * attackRange;
        }


        public bool TryAttack(Transform target)
        {
            if (target == null) return false;

            bool canAttack = Time.time - lastAttackTime >= attackCoolDown && IsInAttackRange(target);

            if (!canAttack) return false;

            lastAttackTime = Time.time;
            //ApplyDamage(target);


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
            float dmg = fallbackDamage;
            if (enemyBase != null && enemyBase.enemyData != null)
            {
                dmg = enemyBase.enemyData.damageAmount;
            }

            StatusEffectManager effectManager = GetComponent<StatusEffectManager>();
            if (effectManager != null)
            {
                dmg *= effectManager.GetOutgoingDamageMultiplier();
            }

            return dmg;
        }

        public void ActivateCollider()
        {
            if(meleeAttackCollider==null) return;
            meleeAttackCollider.Initialize(GetDamageValue());
            meleeAttackCollider.gameObject.SetActive(true);
        }

        public void DeactivateCollider()
        {
            if(meleeAttackCollider==null) return;
            meleeAttackCollider.gameObject.SetActive(false);
        }

        public void ResetForReuse()
        {
            lastAttackTime = -999f;
            DeactivateCollider();
        }
    }
}
