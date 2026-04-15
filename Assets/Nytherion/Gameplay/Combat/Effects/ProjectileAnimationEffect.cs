using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat.Effects
{
    public class ProjectileAnimationEffect : MonoBehaviour, IProjectileEffect
    {
        [Header("Animation Settings")]
        public Animator animator;
        public string triggerName = "Bounce";

        public float squashDuration = 0.15f;

        private bool isSquashing = false;
        private Quaternion squashRotation;
        private Rigidbody2D rb;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            rb = GetComponent<Rigidbody2D>();
        }

        public bool OnHit(Collider2D target)
        {
            Vector2 closestPoint = target.ClosestPoint(transform.position);
            Vector2 normal = ((Vector2)transform.position - closestPoint).normalized;

            if (normal == Vector2.zero && rb != null)
            {
                normal = -rb.velocity.normalized;
            }

            float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
            squashRotation = Quaternion.AngleAxis(angle, Vector3.forward);

            // 3. 찌그러짐 상태 돌입 및 애니메이션 재생
            isSquashing = true;
            if (animator != null)
            {
                animator.SetTrigger(triggerName);
            }

            CancelInvoke(nameof(EndSquash));
            Invoke(nameof(EndSquash), squashDuration);

            return false;
        }

        private void EndSquash()
        {
            isSquashing = false;
        }

        private void LateUpdate()
        {

            if (isSquashing)
            {
                transform.rotation = squashRotation;
            }
            else if (rb != null && rb.velocity.sqrMagnitude > 0.1f)
            {
                float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }
    }
}