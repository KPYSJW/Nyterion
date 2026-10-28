using System;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 플레이어 주변을 떠다니며 범위 안의 적에게 주기적으로 공격을 요청합니다.
    /// 실제 공격 방식은 AttackRequested 또는 Inspector 이벤트에 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GhostCompanion : SummonedCompanion
    {
        [Serializable]
        public sealed class GhostAttackEvent : UnityEvent<Transform> { }

        [Header("공중 배회")]
        [SerializeField] private Vector2 hoverCenter = new Vector2(0f, 0.8f);
        [SerializeField, Min(0f)] private float wanderRadius = 0.65f;
        [SerializeField, Min(0.1f)] private float wanderInterval = 3.5f;
        [SerializeField, Min(0.01f)] private float movementSmoothTime = 0.9f;
        [SerializeField, Min(0f)] private float bobHeight = 0.05f;
        [SerializeField, Min(0f)] private float bobFrequency = 0.5f;
        [Tooltip("원본 스프라이트가 왼쪽을 바라보면 활성화합니다.")]
        [SerializeField] private bool spriteFacesLeft;

        [Header("공격 연결")]
        [Tooltip("공격 모션의 5번째 프레임에 표적을 전달합니다. 피해나 투사체는 연결한 공격 기능에서 처리합니다.")]
        [SerializeField] private GhostAttackEvent onAttackRequested = new GhostAttackEvent();

        [Header("영혼 적중 이펙트")]
        [SerializeField] private GameObject spiritHitEffectPrefab;
        [SerializeField, Min(0.01f)] private float spiritHitEffectLifetime = 0.4f;

        public event Action<Transform> AttackRequested;

        private Vector2 wanderOffset;
        private Vector2 movementVelocity;
        private float nextWanderTime;
        private float bobPhase;
        private Transform pendingTarget;
        private GameObject activeSpiritHitEffect;

        protected override bool IsUsingCustomMovement => true;
        protected override bool CanAutoAttack => attackFreezeTimer <= 0f;

        protected override void OnInitialized()
        {
            // 벽과 장애물의 영향을 받지 않고 떠다닙니다.
            CompanionRigidbody.bodyType = RigidbodyType2D.Kinematic;
            CompanionRigidbody.velocity = Vector2.zero;
            movementVelocity = Vector2.zero;
            bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            ChooseWanderOffset();
        }

        protected override void OnCompanionUpdate(Transform combatTarget)
        {
            // 공격하는 동안 배회 목적지와 바라보는 방향을 유지합니다.
            if (attackFreezeTimer > 0f) return;

            if (Time.time >= nextWanderTime)
            {
                ChooseWanderOffset();
            }

            float horizontalDirection = combatTarget != null
                ? combatTarget.position.x - transform.position.x
                : movementVelocity.x;
            if (spriteRenderer != null && Mathf.Abs(horizontalDirection) > 0.05f)
            {
                spriteRenderer.flipX = (horizontalDirection < 0f) != spriteFacesLeft;
            }
        }

        protected override bool TryHandleCustomFixedUpdate()
        {
            if (attackFreezeTimer > 0f)
            {
                movementVelocity = Vector2.zero;
                CompanionRigidbody.velocity = Vector2.zero;
                return true;
            }

            float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f + bobPhase) * bobHeight;
            Vector2 destination = (Vector2)owner.position + hoverCenter + wanderOffset + Vector2.up * bob;
            Vector2 position = Vector2.SmoothDamp(
                CompanionRigidbody.position,
                destination,
                ref movementVelocity,
                Mathf.Max(0.01f, movementSmoothTime),
                Mathf.Infinity,
                Time.fixedDeltaTime);
            CompanionRigidbody.MovePosition(position);
            return true;
        }

        protected override bool TryAttack(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            // 공격 애니메이션이 끝날 때까지 이동과 재공격을 제한합니다.
            attackFreezeTimer = Mathf.Max(0f, attackFreezeDuration);
            movementVelocity = Vector2.zero;
            CompanionRigidbody.velocity = Vector2.zero;
            pendingTarget = target;
            TriggerAttackAnimation();
            return true;
        }

        /// <summary>공격 모션의 5번째 프레임에서 적중 이펙트와 공격을 실행합니다.</summary>
        public void ReleaseSpiritHit()
        {
            Transform target = pendingTarget;
            pendingTarget = null;
            if (target == null || !target.gameObject.activeInHierarchy) return;

            // 공격 이벤트에서 적이 사라지기 전에 적중 위치와 표시 순서를 확보합니다.
            SpawnSpiritHitEffect(target);
            AttackRequested?.Invoke(target);
            onAttackRequested.Invoke(target);
        }

        /// <summary>공격 모션의 9번째 프레임에서 적중 이펙트를 종료합니다.</summary>
        public void EndSpiritHit()
        {
            GameObject effect = activeSpiritHitEffect;
            activeSpiritHitEffect = null;
            if (effect == null) return;

            if (ObjectPoolManager.Instance != null &&
                ObjectPoolManager.Instance.TryGetPoolTag(effect, out string poolTag))
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, effect);
            }
            else
            {
                Destroy(effect);
            }
        }

        private void OnDisable()
        {
            pendingTarget = null;
            EndSpiritHit();
        }

        private void SpawnSpiritHitEffect(Transform target)
        {
            if (spiritHitEffectPrefab == null) return;
            EndSpiritHit();

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
                // 눈사람과 같이 적 스프라이트 내부의 임의 위치에 표시합니다.
                position = bounds.center + new Vector3(
                    UnityEngine.Random.Range(-0.7f, 0.7f) * bounds.extents.x,
                    UnityEngine.Random.Range(-0.7f, 0.7f) * bounds.extents.y,
                    0f);
            }

            GameObject effect = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.SpawnFromPool(spiritHitEffectPrefab, position, Quaternion.identity)
                : Instantiate(spiritHitEffectPrefab, position, Quaternion.identity);
            if (effect == null) return;
            activeSpiritHitEffect = effect;

            if (effect.TryGetComponent(out SpriteRenderer effectRenderer) && targetRenderer != null)
            {
                effectRenderer.sortingLayerID = targetRenderer.sortingLayerID;
                effectRenderer.sortingOrder = targetRenderer.sortingOrder + 1;
            }
            if (effect.TryGetComponent(out Animator effectAnimator))
            {
                effectAnimator.Rebind();
                effectAnimator.Update(0f);
                effectAnimator.speed = effectAnimator.GetCurrentAnimatorStateInfo(0).length /
                    Mathf.Max(0.01f, spiritHitEffectLifetime);
            }
            if (effect.TryGetComponent(out AutoReturnToPool autoReturn))
            {
                // 반환은 공격 모션의 종료 이벤트 또는 소환수 비활성화 시 처리합니다.
                autoReturn.StopAllCoroutines();
            }
        }

        private void ChooseWanderOffset()
        {
            wanderOffset = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, wanderRadius);
            nextWanderTime = Time.time + Mathf.Max(0.1f, wanderInterval) * UnityEngine.Random.Range(0.8f, 1.2f);
        }
    }
}
