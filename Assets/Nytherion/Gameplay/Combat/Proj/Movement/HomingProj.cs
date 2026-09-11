using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class HomingProj : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float maxSpeed = 12f;
        [SerializeField] private float acceleration = 15f;
        [SerializeField] private float rotateSpeed = 400f;

        [Header("Tracking")]
        [SerializeField] private float initialStraightDuration = 0.2f;
        [SerializeField] private float trackingRadius = 10f;
        [SerializeField] private float targetRefreshInterval = 0.1f;
        [SerializeField] private float closeRangeSteeringDistance = 1f;
        [SerializeField] private LayerMask enemyLayer;

        private Rigidbody2D rb;
        private Transform target;
        private Collider2D targetCollider;
        private float currentSpeed;
        private float currentMaxSpeed;
        private float homingStartTime;
        private float nextTargetRefreshTime;
        private Vector2 launchDirection;

        private static readonly Collider2D[] homingBuffer = new Collider2D[16];

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (enemyLayer.value == 0)
            {
                enemyLayer = LayerMask.GetMask("Enemy");
            }
        }

        public void SetHomingEnabled(bool isEnabled, float launchSpeed)
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            target = null;
            targetCollider = null;
            currentSpeed = Mathf.Max(0f, launchSpeed);
            currentMaxSpeed = Mathf.Max(maxSpeed, currentSpeed);
            homingStartTime = Time.time + Mathf.Max(0f, initialStraightDuration);
            nextTargetRefreshTime = homingStartTime;

            if (rb != null && rb.velocity.sqrMagnitude > 0.0001f)
            {
                launchDirection = rb.velocity.normalized;
            }
            else
            {
                launchDirection = transform.right;
            }

            if (!isEnabled)
            {
                if (rb != null)
                {
                    rb.angularVelocity = 0f;
                }

                enabled = false;
                return;
            }

            enabled = true;
        }

        private void FixedUpdate()
        {
            if (rb == null) return;

            currentSpeed = Mathf.MoveTowards(currentSpeed, currentMaxSpeed, acceleration * Time.fixedDeltaTime);

            if (Time.time < homingStartTime)
            {
                rb.angularVelocity = 0f;
                rb.velocity = launchDirection * currentSpeed;
                return;
            }

            if (!HasValidTarget() || Time.time >= nextTargetRefreshTime)
            {
                FindClosestEnemy();
                nextTargetRefreshTime = Time.time + Mathf.Max(0.01f, targetRefreshInterval);
            }

            if (target != null)
            {
                Vector2 targetPosition = targetCollider != null
                    ? targetCollider.ClosestPoint(rb.position)
                    : target.position;
                Vector2 direction = targetPosition - rb.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    float distance = direction.magnitude;
                    float steeringSpeed = distance <= closeRangeSteeringDistance
                        ? Mathf.Infinity
                        : rotateSpeed;
                    rb.rotation = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, steeringSpeed * Time.fixedDeltaTime);
                }
            }
            else
            {
                rb.angularVelocity = 0f;
            }

            rb.velocity = transform.right * currentSpeed;
        }

        private bool HasValidTarget()
        {
            return target != null && target.gameObject.activeInHierarchy &&
                   ((Vector2)target.position - rb.position).sqrMagnitude <= trackingRadius * trackingRadius;
        }

        private void FindClosestEnemy()
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, trackingRadius, homingBuffer, enemyLayer);
            float closestDistanceSqr = Mathf.Infinity;
            target = null;
            targetCollider = null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = homingBuffer[i];
                if (hit == null || hit.GetComponent<IDamageable>() == null) continue;

                float distanceSqr = ((Vector2)transform.position - (Vector2)hit.transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    target = hit.transform;
                    targetCollider = hit;
                }
            }
        }
    }
}
