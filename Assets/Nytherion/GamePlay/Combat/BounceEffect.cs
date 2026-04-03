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

        private void OnEnable()
        {
            currentBounces = maxBounces;
            hitTargets.Clear();
        }

        public bool OnHit(Collider2D target)
        {
            if (currentBounces <= 0) return false;

            hitTargets.Add(target.transform);

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, bounceRadius);
            Transform nextTarget = null;
            float closestDist = Mathf.Infinity;

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy") && hit.transform != target.transform && !hitTargets.Contains(hit.transform))
                {
                    float dist = Vector2.Distance(transform.position, hit.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
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