using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Characters.Enemy;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class HomingProj : MonoBehaviour
    {
        [Header("유도 이동 기본값")]
        [SerializeField, Min(0f)] private float rotateSpeed = 180f;
        [SerializeField, Min(0f)] private float initialStraightDuration = 0.15f;
        [SerializeField, Min(0f)] private float trackingRadius = 10f;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private bool useConstantSpeed;

        private Rigidbody2D rb;
        private Collider2D targetCollider;
        private MonoBehaviour targetDamageable;
        private float currentSpeed;
        private float currentTurnSpeed;
        private float launchTimeRemaining;
        private float rotationOffset;
        private Vector2 moveDirection;
        private bool homingEnabled;
        private bool initialized;
        private bool constantSpeed;
        private TrailRenderer[] trails;
        private Animator animator;

        // 발사 시에만 기존 Physics2D 탐색 방식을 사용하며 밀집된 적도 후보에서 누락하지 않습니다.
        private static readonly List<Collider2D> homingBuffer = new List<Collider2D>(32);

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            trails = GetComponentsInChildren<TrailRenderer>(true);
            animator = GetComponentInChildren<Animator>();
            if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
        }

        private void OnEnable()
        {
            Vector2 existingVelocity = rb.velocity;
            ClearFlight();
            // 발사자가 속도를 먼저 준 뒤 이 컴포넌트를 추가하는 기존 경로도 보존합니다.
            rb.velocity = existingVelocity;
            ClearTrails();
            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        // 기존 동료/유물 발사 경로도 발사 시 한 번만 목표를 선택합니다.
        public void SetHomingEnabled(bool isEnabled, float launchSpeed)
        {
            Vector2 direction = rb.velocity.sqrMagnitude > 0.0001f ? rb.velocity.normalized : (Vector2)transform.right;
            Collider2D selectedTarget = isEnabled ? FindClosestEnemy(rb.position, trackingRadius, enemyLayer) : null;
            Initialize(isEnabled, launchSpeed, direction, selectedTarget, rotateSpeed, initialStraightDuration, 0f, useConstantSpeed);
        }

        public void Initialize(bool isEnabled, float speed, Vector2 direction, Collider2D selectedTarget,
            float turnSpeed, float straightDuration, float spriteRotationOffset, bool keepSpeed = false)
        {
            enabled = true;
            targetCollider = selectedTarget;
            targetDamageable = selectedTarget != null
                ? selectedTarget.GetComponentInParent<IDamageable>() as MonoBehaviour
                : null;
            homingEnabled = isEnabled;
            currentSpeed = Mathf.Max(0f, speed);
            currentTurnSpeed = Mathf.Max(0f, turnSpeed);
            launchTimeRemaining = Mathf.Max(0f, straightDuration);
            rotationOffset = spriteRotationOffset;
            constantSpeed = keepSpeed;
            moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            initialized = true;
            rb.angularVelocity = 0f;
            rb.velocity = moveDirection * currentSpeed;
            FaceDirection();
            ClearTrails();
        }

        public void StopTrackingHitTarget(IDamageable hitTarget)
        {
            if (!ReferenceEquals(targetDamageable, hitTarget)) return;
            // 관통 후에도 살아 있는 적(허수아비 등)을 다시 향해 돌아오지 않습니다.
            targetCollider = null;
            targetDamageable = null;
        }

        private void FixedUpdate()
        {
            if (!initialized) return;

            // 관통/튕김 등 기존 충돌 모듈이 바꾼 속도 방향도 다음 물리 틱의 기준으로 사용합니다.
            if (rb.velocity.sqrMagnitude > 0.0001f) moveDirection = rb.velocity.normalized;
            if (homingEnabled)
            {
                float flightSpeed = currentSpeed;
                if (!IsAlive(targetCollider, targetDamageable))
                {
                    // 사망/비활성화 시 참조를 버리므로 적이 풀에서 재사용돼도 다시 추적하지 않습니다.
                    targetCollider = null;
                    targetDamageable = null;
                }

                if (launchTimeRemaining <= 0f && targetCollider != null)
                {
                    Vector2 desired = (Vector2)targetCollider.bounds.center - rb.position;
                    if (desired.sqrMagnitude > 0.0001f)
                    {
                        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                        float desiredAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
                        float turnSpeed = currentTurnSpeed;
                        if (constantSpeed && turnSpeed > 0f)
                        {
                            // 일정 속도에서도 가까운 적을 맴돌지 않도록 회전 반경을 목표 거리 안으로 줄입니다.
                            float distance = Mathf.Max(desired.magnitude, currentSpeed * Time.fixedDeltaTime);
                            turnSpeed = Mathf.Max(turnSpeed, currentSpeed / (distance * 0.65f) * Mathf.Rad2Deg);
                        }
                        float nextAngle = Mathf.MoveTowardsAngle(angle, desiredAngle,
                            turnSpeed * Time.fixedDeltaTime) * Mathf.Deg2Rad;
                        moveDirection = new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle));

                        if (!constantSpeed && currentTurnSpeed > 0f)
                        {
                            // 속도/회전 속도로 정해지는 회전 반경이 목표 거리보다 크면 원운동하게 됩니다.
                            // 방향 차이가 큰 근거리에서만 감속하고, 정렬되면 원래 속도로 접근합니다.
                            float distance = desired.magnitude;
                            float turnLimitedSpeed = distance * currentTurnSpeed * Mathf.Deg2Rad * 0.65f;
                            float headingError = Vector2.Angle(moveDirection, desired);
                            float braking = Mathf.InverseLerp(5f, 60f, headingError);
                            flightSpeed = Mathf.Lerp(currentSpeed, Mathf.Min(currentSpeed, turnLimitedSpeed), braking);
                            // 한 물리 틱에 목표 중심을 지나치지 않도록 이동량도 제한합니다.
                            flightSpeed = Mathf.Min(flightSpeed, distance / Time.fixedDeltaTime);
                        }
                    }
                }
                launchTimeRemaining -= Time.fixedDeltaTime;
                rb.velocity = moveDirection * flightSpeed;
            }
            FaceDirection();
        }

        private void FaceDirection()
        {
            rb.rotation = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg + rotationOffset;
        }

        public static Collider2D FindClosestEnemy(Vector2 origin, float radius, LayerMask layers = default)
        {
            if (radius <= 0f) return null;
            if (layers.value == 0) layers = LayerMask.GetMask("Enemy");
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(layers);
            homingBuffer.Clear();
            int hitCount = Physics2D.OverlapCircle(origin, radius, filter, homingBuffer);
            float closestDistanceSqr = float.PositiveInfinity;
            Collider2D closest = null;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = homingBuffer[i];
                MonoBehaviour damageable = hit != null ? hit.GetComponentInParent<IDamageable>() as MonoBehaviour : null;
                if (!IsAlive(hit, damageable)) continue;
                // 자식 감지용 트리거를 목표로 삼지 않고 실제 적 몸체를 선택합니다.
                if (hit.isTrigger && hit.transform != damageable.transform) continue;
                float distanceSqr = ((Vector2)hit.bounds.center - origin).sqrMagnitude;
                if (distanceSqr > radius * radius || distanceSqr >= closestDistanceSqr) continue;
                closestDistanceSqr = distanceSqr;
                closest = hit;
            }
            homingBuffer.Clear();
            return closest;
        }

        private static bool IsAlive(Collider2D collider, MonoBehaviour damageable)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
                damageable != null && damageable.isActiveAndEnabled &&
                (!(damageable is EnemyBase enemy) || !enemy.isDead);
        }

        private void ClearTrails()
        {
            if (trails == null) return;
            foreach (TrailRenderer trail in trails) trail.Clear();
        }

        private void ClearFlight()
        {
            targetCollider = null;
            targetDamageable = null;
            moveDirection = Vector2.zero;
            currentSpeed = launchTimeRemaining = rotationOffset = 0f;
            homingEnabled = initialized = constantSpeed = false;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void OnDisable()
        {
            ClearFlight();
            ClearTrails();
        }
    }
}
