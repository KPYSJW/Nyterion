using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class BounceEffect : MonoBehaviour, IProjectileEffect
    {
        public int maxBounces = 3;
        public float bounceRadius = 5f;

        private int currentBounces;
        private HashSet<Transform> hitTargets = new HashSet<Transform>();
        private static readonly Collider2D[] bounceBuffer = new Collider2D[10];

        private void OnEnable()
        {
            currentBounces = maxBounces;
            hitTargets.Clear();
        }

        public bool OnHit(Collider2D target)
        {
            if (currentBounces <= 0) return false;

            hitTargets.Add(target.transform);

            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, bounceRadius, bounceBuffer);
            Transform nextTarget = null;
            float closestDistSqr = Mathf.Infinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = bounceBuffer[i];
                if (hit.CompareTag("Enemy") && hit.transform != target.transform && !hitTargets.Contains(hit.transform))
                {
                    float distSqr = (transform.position - hit.transform.position).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        nextTarget = hit.transform;
                    }
                }
            }

            if (nextTarget != null)
            {
                currentBounces--;
                Vector2 direction = (nextTarget.position - transform.position).normalized;

                if (TryGetComponent<Rigidbody2D>(out var rb))
                {
                    float currentSpeed = rb.velocity.magnitude;
                    rb.velocity = direction * currentSpeed;
                }

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

                return true; 
            }

            return false; 
        }
    }
}