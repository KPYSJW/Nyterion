using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 소환수 공격 이펙트의 Collider 영역에 들어온 적에게 한 번만 피해를 적용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionAttackEffect : MonoBehaviour
    {
        private readonly Collider2D[] overlapBuffer = new Collider2D[20];
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

        private Collider2D attackCollider;
        private float damage;
        private bool isInitialized;

        private void Awake()
        {
            attackCollider = GetComponent<CapsuleCollider2D>();
            if (attackCollider == null)
            {
                attackCollider = GetComponent<Collider2D>();
            }
        }

        private void OnEnable()
        {
            isInitialized = false;
            hitTargets.Clear();
        }

        public void Initialize(float attackDamage)
        {
            if (attackCollider == null)
            {
                attackCollider = GetComponent<Collider2D>();
            }

            damage = Mathf.Max(0f, attackDamage);
            hitTargets.Clear();
            isInitialized = attackCollider != null;
            ApplyDamageToOverlappingTargets();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isInitialized)
            {
                TryDamage(other);
            }
        }

        private void ApplyDamageToOverlappingTargets()
        {
            if (!isInitialized)
            {
                return;
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.NoFilter();
            filter.useTriggers = true;
            int hitCount = attackCollider.OverlapCollider(filter, overlapBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                TryDamage(overlapBuffer[i]);
            }
        }

        private void TryDamage(Collider2D other)
        {
            if (other == null || !other.CompareTag("Enemy") ||
                !other.TryGetComponent(out IDamageable damageable) || !hitTargets.Add(damageable))
            {
                return;
            }

            damageable.TakeDamage(damage);
        }
    }
}
