using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Combat; // CollisionObject 사용을 위해 추가

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class MeteorProjectile : MonoBehaviour
    {
        [Header("Settings")]
        public float fallSpeed = 20f;
        public float explosionRadius = 2.5f;

        private Vector3 targetPosition;
        private bool isFalling = false;

        private Animator animator;
        private CollisionObject col;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            col = GetComponent<CollisionObject>();
        }

        public void Initialize(Vector3 targetPos)
        {
            targetPosition = targetPos;
            isFalling = true;
        }

        private void Update()
        {
            if (!isFalling) return;

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, fallSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) <= 0.1f)
            {
                TriggerExplosion();
            }
        }

        private void TriggerExplosion()
        {
            isFalling = false; 

            if (animator != null)
            {
                animator.SetTrigger("Explode");
            }
            else
            {
                ApplyDamage();
                DisableProjectile();
            }
        }

        public void ApplyDamage()
        {
            float finalDamage = col != null ? col.damage : 10f;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    if (hit.TryGetComponent<IDamageable>(out var damageable))
                    {
                        damageable.TakeDamage(finalDamage);
                    }
                }
            }
        }
        public void DisableProjectile()
        {
            gameObject.SetActive(false);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}