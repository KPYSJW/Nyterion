using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat.Effects
{
    public class ReflectionBounceEffect : MonoBehaviour, IProjectileEffect
    {
        [Header("Reflection Settings")]
        public int maxBounces = 3;         
        public float maxLifetime = 5f;     

        private int currentBounces;
        private float lifeTimer;
        private CollisionObject collisionObj;

        private void Awake()
        {
            collisionObj = GetComponent<CollisionObject>();
        }

        private void OnEnable()
        {
            currentBounces = maxBounces;
            lifeTimer = 0f;
        }

        private void Update()
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= maxLifetime && collisionObj != null)
            {
                collisionObj.ReturnToPool();
            }
        }

        public bool OnHit(Collider2D target)
        {
            if (currentBounces <= 0) return false;

            Vector2 closestPoint = target.ClosestPoint(transform.position);
            Vector2 normal = ((Vector2)transform.position - closestPoint).normalized;

            if (normal == Vector2.zero && TryGetComponent<Rigidbody2D>(out var rbFallback))
            {
                normal = -rbFallback.velocity.normalized;
            }

            if (TryGetComponent<Rigidbody2D>(out var rb))
            {
                Vector2 reflectedDir = Vector2.Reflect(rb.velocity.normalized, normal);
                float speed = rb.velocity.magnitude;
                rb.velocity = reflectedDir * speed;

                float angle = Mathf.Atan2(reflectedDir.y, reflectedDir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            currentBounces--;
            return true;
        }
    }
}