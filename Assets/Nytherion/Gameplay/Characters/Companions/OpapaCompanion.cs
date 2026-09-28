using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Systems;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 적을 향해 구른 뒤 벽, 장애물과 카메라 경계에서 반사되는 Opapa 소환수입니다.
    /// 구르는 동안 적을 관통하며 한 번씩 피해를 줍니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class OpapaCompanion : SummonedCompanion
    {
        private enum RollState
        {
            None,
            Starting,
            Rolling,
            Stopping
        }

        [Header("구르기 공격")]
        [SerializeField, Min(1f)] private float rollingSpeedMultiplier = 2.25f;
        [SerializeField, Min(0.1f)] private float rollingDuration = 3f;
        [SerializeField, Min(0f)] private float rollStartFallbackDuration = 0.25f;
        [SerializeField, Min(0f)] private float rollStopFallbackDuration = 0.25f;
        [SerializeField, Min(0f)] private float collisionSkin = 0.02f;
        [SerializeField] private LayerMask blockingLayers;
        [SerializeField, Range(1, 8)] private int maximumBouncesPerStep = 4;

        [Header("전용 애니메이션")]
        [SerializeField] private string blinkTriggerParam = "Blink";
        [SerializeField] private string rollStopTriggerParam = "StopRoll";
        [SerializeField, Min(0.1f)] private float minimumBlinkInterval = 3f;
        [SerializeField, Min(0.1f)] private float maximumBlinkInterval = 7f;

        private static readonly RaycastHit2D[] BlockingHitBuffer = new RaycastHit2D[16];
        private static readonly RaycastHit2D[] EnemySweepBuffer = new RaycastHit2D[32];
        private static readonly Collider2D[] EnemyOverlapBuffer = new Collider2D[32];

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private CircleCollider2D bodyCollider;
        private Camera gameplayCamera;
        private RollState rollState;
        private Vector2 rollingDirection;
        private float rollingDamage;
        private float phaseEndTime;
        private float nextBlinkTime;
        private bool wasIdle;

        protected override bool IsUsingCustomMovement => rollState != RollState.None;
        protected override bool CanAutoAttack => rollState == RollState.None;

        protected override void OnInitialized()
        {
            CacheOpapaComponents();
            rollState = RollState.None;
            damagedTargets.Clear();
            wasIdle = false;
            ScheduleNextBlink();
        }

        protected override void OnCompanionUpdate(Transform combatTarget)
        {
            switch (rollState)
            {
                case RollState.Starting when Time.time >= phaseEndTime:
                    BeginRolling();
                    break;
                case RollState.Rolling when Time.time >= phaseEndTime:
                    BeginRollStop();
                    break;
                case RollState.Stopping when Time.time >= phaseEndTime:
                    CompleteRollAttack();
                    break;
                case RollState.None:
                    UpdateBlink();
                    break;
            }
        }

        protected override bool TryAttack(Transform target)
        {
            if (rollState != RollState.None || target == null)
            {
                return false;
            }

            CacheOpapaComponents();
            Vector2 targetDirection = (Vector2)target.position - CompanionRigidbody.position;
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                targetDirection = transform.right;
            }

            rollingDirection = targetDirection.normalized;
            rollingDamage = GetProjectileDamage(ownerCombat != null ? ownerCombat.currentWeapon : null);
            damagedTargets.Clear();
            rollState = RollState.Starting;
            phaseEndTime = Time.time + Mathf.Max(0f, rollStartFallbackDuration);
            attackFreezeTimer = Mathf.Max(
                attackFreezeTimer,
                rollStartFallbackDuration + rollingDuration + rollStopFallbackDuration);
            SetMoving(false);
            ResetAnimationTrigger(blinkTriggerParam);
            TriggerAttackAnimation();
            UpdateRollingFacing();
            return true;
        }

        protected override bool TryHandleCustomFixedUpdate()
        {
            if (rollState == RollState.None)
            {
                return false;
            }

            CacheOpapaComponents();
            CompanionRigidbody.velocity = Vector2.zero;
            if (rollState != RollState.Rolling || rollingDirection.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            MoveAndBounce(MaxMoveSpeed * rollingSpeedMultiplier * Time.fixedDeltaTime);
            return true;
        }

        /// <summary>
        /// Roll_Start 마지막 프레임의 Animation Event에서 호출합니다.
        /// </summary>
        public void BeginRolling()
        {
            if (rollState != RollState.Starting)
            {
                return;
            }

            rollState = RollState.Rolling;
            phaseEndTime = Time.time + Mathf.Max(0.1f, rollingDuration);
            DamageOverlappingEnemies(CompanionRigidbody.position);
        }

        /// <summary>
        /// Roll_Stop 마지막 프레임의 Animation Event에서 호출합니다.
        /// </summary>
        public void CompleteRollAttack()
        {
            if (rollState != RollState.Stopping)
            {
                return;
            }

            rollState = RollState.None;
            rollingDirection = Vector2.zero;
            damagedTargets.Clear();
            attackFreezeTimer = 0f;
            CompanionRigidbody.velocity = Vector2.zero;
            wasIdle = false;
            ScheduleNextBlink();
        }

        private void BeginRollStop()
        {
            if (rollState != RollState.Rolling)
            {
                return;
            }

            rollState = RollState.Stopping;
            phaseEndTime = Time.time + Mathf.Max(0f, rollStopFallbackDuration);
            CompanionRigidbody.velocity = Vector2.zero;
            TriggerAnimation(rollStopTriggerParam);
        }

        private void MoveAndBounce(float travelDistance)
        {
            if (travelDistance <= 0f)
            {
                return;
            }

            Vector2 position = CompanionRigidbody.position;
            float radius = GetWorldRadius();
            float remainingDistance = travelDistance;
            int bounceCount = 0;

            while (remainingDistance > 0.0001f && bounceCount <= maximumBouncesPerStep)
            {
                float blockingDistance = FindBlockingHit(
                    position,
                    rollingDirection,
                    radius,
                    remainingDistance,
                    out Vector2 blockingNormal);
                float cameraDistance = FindCameraBoundaryHit(
                    position,
                    rollingDirection,
                    radius,
                    remainingDistance,
                    out Vector2 cameraNormal);

                float hitDistance = Mathf.Min(blockingDistance, cameraDistance);
                if (float.IsPositiveInfinity(hitDistance) || hitDistance > remainingDistance)
                {
                    DamageEnemiesAlongPath(position, rollingDirection, remainingDistance, radius);
                    position += rollingDirection * remainingDistance;
                    remainingDistance = 0f;
                    break;
                }

                Vector2 hitNormal = blockingDistance <= cameraDistance
                    ? blockingNormal
                    : cameraNormal;
                float safeDistance = Mathf.Max(0f, hitDistance - collisionSkin);
                DamageEnemiesAlongPath(position, rollingDirection, safeDistance, radius);
                position += rollingDirection * safeDistance;
                position += hitNormal * collisionSkin;

                float consumedDistance = Mathf.Max(hitDistance, collisionSkin);
                remainingDistance = Mathf.Max(0f, remainingDistance - consumedDistance);
                rollingDirection = Vector2.Reflect(rollingDirection, hitNormal).normalized;
                bounceCount++;
            }

            CompanionRigidbody.MovePosition(position);
            DamageOverlappingEnemies(position);
            UpdateRollingFacing();
        }

        private float FindBlockingHit(
            Vector2 origin,
            Vector2 direction,
            float radius,
            float distance,
            out Vector2 hitNormal)
        {
            hitNormal = Vector2.zero;
            int layerMask = blockingLayers.value != 0 ? blockingLayers.value : Physics2D.AllLayers;
            int hitCount = Physics2D.CircleCastNonAlloc(
                origin,
                radius,
                direction,
                BlockingHitBuffer,
                distance + collisionSkin,
                layerMask);
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = BlockingHitBuffer[i];
                if (hit.collider == null || hit.collider == bodyCollider || !IsBlockingCollider(hit.collider) ||
                    hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = Mathf.Max(0f, hit.distance);
                hitNormal = hit.normal.sqrMagnitude > 0.0001f
                    ? hit.normal.normalized
                    : -direction;
            }

            return closestDistance;
        }

        private float FindCameraBoundaryHit(
            Vector2 origin,
            Vector2 direction,
            float radius,
            float distance,
            out Vector2 hitNormal)
        {
            hitNormal = Vector2.zero;
            if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled)
            {
                gameplayCamera = Camera.main;
            }
            if (gameplayCamera == null)
            {
                return float.PositiveInfinity;
            }

            float depth = Mathf.Abs(transform.position.z - gameplayCamera.transform.position.z);
            Vector3 viewportMin = gameplayCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 viewportMax = gameplayCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            float minX = Mathf.Min(viewportMin.x, viewportMax.x) + radius;
            float maxX = Mathf.Max(viewportMin.x, viewportMax.x) - radius;
            float minY = Mathf.Min(viewportMin.y, viewportMax.y) + radius;
            float maxY = Mathf.Max(viewportMin.y, viewportMax.y) - radius;

            float closestDistance = float.PositiveInfinity;
            if (direction.x > 0.0001f)
            {
                TrySelectBoundary((maxX - origin.x) / direction.x, Vector2.left,
                    distance, ref closestDistance, ref hitNormal);
            }
            else if (direction.x < -0.0001f)
            {
                TrySelectBoundary((minX - origin.x) / direction.x, Vector2.right,
                    distance, ref closestDistance, ref hitNormal);
            }

            if (direction.y > 0.0001f)
            {
                TrySelectBoundary((maxY - origin.y) / direction.y, Vector2.down,
                    distance, ref closestDistance, ref hitNormal);
            }
            else if (direction.y < -0.0001f)
            {
                TrySelectBoundary((minY - origin.y) / direction.y, Vector2.up,
                    distance, ref closestDistance, ref hitNormal);
            }

            return closestDistance;
        }

        private static void TrySelectBoundary(
            float candidateDistance,
            Vector2 candidateNormal,
            float maximumDistance,
            ref float closestDistance,
            ref Vector2 hitNormal)
        {
            candidateDistance = Mathf.Max(0f, candidateDistance);
            if (candidateDistance <= maximumDistance && candidateDistance < closestDistance)
            {
                closestDistance = candidateDistance;
                hitNormal = candidateNormal;
            }
        }

        private void DamageEnemiesAlongPath(
            Vector2 origin,
            Vector2 direction,
            float distance,
            float radius)
        {
            DamageOverlappingEnemies(origin);
            if (distance <= 0.0001f)
            {
                return;
            }

            int hitCount = Physics2D.CircleCastNonAlloc(
                origin,
                radius,
                direction,
                EnemySweepBuffer,
                distance,
                Physics2D.AllLayers);
            for (int i = 0; i < hitCount; i++)
            {
                TryDamageEnemy(EnemySweepBuffer[i].collider);
            }
        }

        private void DamageOverlappingEnemies(Vector2 position)
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(
                position,
                GetWorldRadius(),
                EnemyOverlapBuffer,
                Physics2D.AllLayers);
            for (int i = 0; i < hitCount; i++)
            {
                TryDamageEnemy(EnemyOverlapBuffer[i]);
            }
        }

        private void TryDamageEnemy(Collider2D hit)
        {
            if (rollState != RollState.Rolling || hit == null)
            {
                return;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            MonoBehaviour damageableBehaviour = damageable as MonoBehaviour;
            if (damageableBehaviour == null || !damageableBehaviour.isActiveAndEnabled ||
                (!hit.CompareTag(Tags.Enemy) && !damageableBehaviour.CompareTag(Tags.Enemy)) ||
                !damagedTargets.Add(damageable))
            {
                return;
            }

            damageable.TakeDamage(rollingDamage);
        }

        private bool IsBlockingCollider(Collider2D candidate)
        {
            int candidateLayer = 1 << candidate.gameObject.layer;
            return (blockingLayers.value & candidateLayer) != 0 || candidate.CompareTag(Tags.Wall);
        }

        private float GetWorldRadius()
        {
            CacheOpapaComponents();
            Vector3 extents = bodyCollider.bounds.extents;
            return Mathf.Max(0.01f, Mathf.Max(extents.x, extents.y));
        }

        private void CacheOpapaComponents()
        {
            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<CircleCollider2D>();
                bodyCollider.isTrigger = true;
            }

            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
        }

        private void UpdateBlink()
        {
            bool isIdle = !IsMoving;
            if (!isIdle)
            {
                wasIdle = false;
                return;
            }

            if (!wasIdle)
            {
                wasIdle = true;
                ScheduleNextBlink();
            }

            if (Time.time < nextBlinkTime)
            {
                return;
            }

            TriggerAnimation(blinkTriggerParam);
            ScheduleNextBlink();
        }

        private void ScheduleNextBlink()
        {
            float minimum = Mathf.Max(0.1f, minimumBlinkInterval);
            float maximum = Mathf.Max(minimum, maximumBlinkInterval);
            nextBlinkTime = Time.time + Random.Range(minimum, maximum);
        }

        private void UpdateRollingFacing()
        {
            if (spriteRenderer != null && Mathf.Abs(rollingDirection.x) > 0.01f)
            {
                spriteRenderer.flipX = rollingDirection.x < 0f;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamageEnemy(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamageEnemy(other);
        }

        private void OnDisable()
        {
            rollState = RollState.None;
            damagedTargets.Clear();
            if (CompanionRigidbody != null)
            {
                CompanionRigidbody.velocity = Vector2.zero;
            }
        }

        private void OnValidate()
        {
            rollingSpeedMultiplier = Mathf.Max(1f, rollingSpeedMultiplier);
            rollingDuration = Mathf.Max(0.1f, rollingDuration);
            maximumBouncesPerStep = Mathf.Clamp(maximumBouncesPerStep, 1, 8);
            maximumBlinkInterval = Mathf.Max(minimumBlinkInterval, maximumBlinkInterval);
        }
    }
}
