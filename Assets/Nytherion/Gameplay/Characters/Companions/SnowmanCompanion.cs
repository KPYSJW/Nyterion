using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>플레이어를 따라다니며 가까운 적에게 냉기를 내뿜는 눈사람입니다.</summary>
    [DisallowMultipleComponent]
    public sealed class SnowmanCompanion : SummonedCompanion
    {
        [Header("냉기 공격")]
        [SerializeField, Min(0.1f)] private float frostRange = 1.8f;
        [SerializeField, Min(0.1f)] private float chillDuration = 3f;
        [SerializeField, Min(0.1f)] private float dismissDuration = 1.7f;

        [Header("냉기 적중 이펙트")]
        [SerializeField] private GameObject frostHitEffectPrefab;
        [SerializeField, Min(0.01f)] private float frostHitEffectLifetime = 0.5f;

        private Transform pendingTarget;
        private bool isDismissing;
        private bool isAttacking;
        private bool isFrostPending;

        protected override bool ShouldPursueCombatTarget => true;
        protected override float CombatApproachDistance => frostRange * 0.75f;
        protected override float CombatStopDistance => frostRange * 0.75f;
        protected override bool CanAutoAttack => !isDismissing && !isAttacking && attackFreezeTimer <= 0f;

        protected override Transform FindPriorityTarget()
        {
            // 공격을 시작한 뒤에는 다른 적을 바라보며 공격하지 않습니다.
            return isAttacking ? pendingTarget : base.FindPriorityTarget();
        }

        protected override void OnCompanionUpdate(Transform combatTarget)
        {
            if (isAttacking && attackFreezeTimer <= 0f)
            {
                isAttacking = false;
                isFrostPending = false;
                pendingTarget = null;
                // 공격 동작이 끝난 시점부터 다음 공격까지의 대기 시간을 계산합니다.
                nextAttackTime = Time.time + GetAttackInterval();
            }
        }

        protected override bool TryAttack(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy || isDismissing || isAttacking ||
                Vector2.Distance(transform.position, target.position) > frostRange)
            {
                return false;
            }

            pendingTarget = target;
            isAttacking = true;
            isFrostPending = true;
            attackFreezeTimer = attackFreezeDuration;
            SetMoving(false);
            TriggerAttackAnimation();
            return true;
        }

        /// <summary>냉기가 펼쳐지는 프레임에서 한 번만 피해와 서리를 적용합니다.</summary>
        public void ReleaseFrost()
        {
            if (!isFrostPending) return;
            isFrostPending = false;
            Transform target = pendingTarget;
            if (isDismissing || owner == null || target == null || !target.gameObject.activeInHierarchy ||
                Vector2.Distance(transform.position, target.position) > frostRange ||
                !target.TryGetComponent(out IDamageable damageable))
            {
                return;
            }

            // 피해로 적이 사라져도 적중 위치와 표시 순서를 먼저 확보합니다.
            SpawnFrostHitEffect(target);
            damageable.TakeDamage(GetProjectileDamage(ownerCombat != null ? ownerCombat.currentWeapon : null));
            if (target != null && target.gameObject.activeInHierarchy && target.TryGetComponent(out StatusEffectManager effects))
            {
                effects.ApplyEffect(new IceEffect(chillDuration));
            }
        }

        private void SpawnFrostHitEffect(Transform target)
        {
            if (frostHitEffectPrefab == null) return;

            SpriteRenderer targetRenderer = target.GetComponent<SpriteRenderer>();
            if (targetRenderer == null)
            {
                Animator targetAnimator = target.GetComponentInChildren<Animator>();
                targetRenderer = targetAnimator != null
                    ? targetAnimator.GetComponentInChildren<SpriteRenderer>()
                    : target.GetComponentInChildren<SpriteRenderer>();
            }

            Vector3 position = target.position;
            if (targetRenderer != null && targetRenderer.sprite != null)
            {
                Bounds bounds = targetRenderer.bounds;
                // 외곽의 투명 여백을 피하도록 스프라이트 내부에서 중심점을 뽑습니다.
                position = bounds.center + new Vector3(
                    Random.Range(-0.7f, 0.7f) * bounds.extents.x,
                    Random.Range(-0.7f, 0.7f) * bounds.extents.y,
                    0f);
            }

            GameObject effect = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.SpawnFromPool(frostHitEffectPrefab, position, Quaternion.identity)
                : Instantiate(frostHitEffectPrefab, position, Quaternion.identity);
            if (effect == null) return;

            if (effect.TryGetComponent(out SpriteRenderer effectRenderer) && targetRenderer != null)
            {
                effectRenderer.sortingLayerID = targetRenderer.sortingLayerID;
                effectRenderer.sortingOrder = targetRenderer.sortingOrder + 1;
            }
            if (effect.TryGetComponent(out Animator effectAnimator))
            {
                effectAnimator.Rebind();
                effectAnimator.Update(0f);
            }
            if (ObjectPoolManager.Instance != null && effect.TryGetComponent(out AutoReturnToPool autoReturn))
            {
                autoReturn.InitializeDelay(frostHitEffectLifetime);
            }
            else
            {
                Destroy(effect, frostHitEffectLifetime);
            }
        }

        public override void Dismiss()
        {
            if (isDismissing) return;
            isDismissing = true;
            isAttacking = false;
            isFrostPending = false;
            pendingTarget = null;
            SetMoving(false);
            CompanionRigidbody.velocity = Vector2.zero;
            ResetAnimationTrigger("Attack1");
            TriggerAnimation("Dismiss");
            // 이동과 공격 갱신만 중단하고 Animator는 퇴장 연출을 계속 재생합니다.
            enabled = false;
            Destroy(gameObject, dismissDuration);
        }

        private void OnDisable()
        {
            pendingTarget = null;
            isAttacking = false;
            isFrostPending = false;
        }
    }
}
