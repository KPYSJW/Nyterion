using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 우선 표적에게 접근해 Croak 애니메이션 프레임에 범위 공격을 수행하는 개구리 소환수입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrogCompanion : SummonedCompanion
    {
        [Header("근접 공격")]
        [SerializeField] private GameObject attackEffectPrefab;
        [SerializeField] private float targetDetectionRange = 6f;
        [SerializeField] private float playerAttackTargetRadius = 2f;
        [SerializeField] private float meleeApproachDistance = 0.4f;
        [SerializeField] private float meleeAttackRange = 0.4f;
        [SerializeField] private float attackEffectLifetime = 0.5f;
        [SerializeField] private int attackEffectSortingOrderOffset = -1;

        private Transform pendingMeleeTarget;

        protected override bool ShouldPursueCombatTarget => true;
        protected override float CombatApproachDistance => meleeApproachDistance;
        protected override float CombatStopDistance => meleeAttackRange;

        protected override bool TryAttack(Transform target)
        {
            if (target == null || Vector2.Distance(transform.position, target.position) > meleeAttackRange)
            {
                return false;
            }

            attackFreezeTimer = attackFreezeDuration;
            SetMoving(false);
            pendingMeleeTarget = target;
            TriggerAttackAnimation();
            return true;
        }

        protected override Transform FindPriorityTarget()
        {
            if (owner == null)
            {
                return null;
            }

            int hitCount = Physics2D.OverlapCircleNonAlloc(owner.position, targetDetectionRange, EnemyBuffer);
            Transform playerAttackTarget = null;
            Transform playerAttackerTarget = null;
            Transform closestTarget = null;
            float closestDistanceSqr = Mathf.Infinity;
            float closestAttackPositionDistanceSqr = Mathf.Infinity;
            float playerAttackTargetRadiusSqr = playerAttackTargetRadius * playerAttackTargetRadius;
            bool hasRecentAttack = Time.time - lastPlayerAttackTime < 3f;
            bool hasRecentDamage = Time.time - lastPlayerHurtTime < 3f;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = EnemyBuffer[i];
                if (hit == null || !hit.CompareTag("Enemy") || hit.GetComponent<IDamageable>() == null)
                {
                    continue;
                }

                float attackPositionDistanceSqr = (hit.transform.position - lastPlayerAttackPosition).sqrMagnitude;
                if (hasRecentAttack && attackPositionDistanceSqr <= playerAttackTargetRadiusSqr &&
                    attackPositionDistanceSqr < closestAttackPositionDistanceSqr)
                {
                    closestAttackPositionDistanceSqr = attackPositionDistanceSqr;
                    playerAttackTarget = hit.transform;
                }

                if (hasRecentDamage && hit.transform == defensiveTarget)
                {
                    playerAttackerTarget = hit.transform;
                }

                float distanceSqr = (hit.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestTarget = hit.transform;
                }
            }

            return playerAttackTarget != null
                ? playerAttackTarget
                : playerAttackerTarget != null
                    ? playerAttackerTarget
                    : closestTarget;
        }

        /// <summary>
        /// Croak 애니메이션의 공격 프레임 Animation Event에서 호출합니다.
        /// </summary>
        public void ExecuteMeleeAttackEffect()
        {
            if (attackEffectPrefab == null || pendingMeleeTarget == null)
            {
                return;
            }

            float damage = GetProjectileDamage(ownerCombat != null ? ownerCombat.currentWeapon : null);
            SpawnAttackEffect(transform.position, damage);
            pendingMeleeTarget = null;
        }

        protected override void FindPlayerAttacker()
        {
            if (owner == null)
            {
                return;
            }

            int hitCount = Physics2D.OverlapCircleNonAlloc(owner.position, targetDetectionRange, EnemyBuffer);
            float closestDistanceSqr = Mathf.Infinity;
            defensiveTarget = null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = EnemyBuffer[i];
                if (hit == null || !hit.CompareTag("Enemy") || hit.GetComponent<IDamageable>() == null)
                {
                    continue;
                }

                float distanceSqr = (owner.position - hit.transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    defensiveTarget = hit.transform;
                }
            }

            if (defensiveTarget != null)
            {
                lastPlayerHurtTime = Time.time;
            }
        }

        private void SpawnAttackEffect(Vector3 position, float damage)
        {
            GameObject effectObject = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.SpawnFromPool(attackEffectPrefab, position, Quaternion.identity)
                : Instantiate(attackEffectPrefab, position, Quaternion.identity);
            if (effectObject == null)
            {
                return;
            }

            if (effectObject.TryGetComponent(out Animator effectAnimator))
            {
                effectAnimator.Rebind();
                effectAnimator.Update(0f);
            }

            if (spriteRenderer != null && effectObject.TryGetComponent(out SpriteRenderer effectRenderer))
            {
                effectRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                effectRenderer.sortingOrder = spriteRenderer.sortingOrder + attackEffectSortingOrderOffset;
            }

            CompanionAttackEffect attackEffect = effectObject.GetComponent<CompanionAttackEffect>();
            if (attackEffect == null)
            {
                attackEffect = effectObject.AddComponent<CompanionAttackEffect>();
            }
            attackEffect.Initialize(damage);

            if (ObjectPoolManager.Instance != null)
            {
                AutoReturnToPool autoReturn = effectObject.GetComponent<AutoReturnToPool>();
                if (autoReturn == null)
                {
                    autoReturn = effectObject.AddComponent<AutoReturnToPool>();
                }
                autoReturn.InitializeDelay(attackEffectLifetime);
            }
            else
            {
                Destroy(effectObject, attackEffectLifetime);
            }
        }
    }
}
