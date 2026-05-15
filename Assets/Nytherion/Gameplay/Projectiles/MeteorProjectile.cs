using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Combat; // CollisionObject ����� ���� �߰�

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
        private static readonly Collider2D[] meteorBuffer = new Collider2D[20];

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

            if ((transform.position - targetPosition).sqrMagnitude <= 0.01f) // 0.1 * 0.1 = 0.01
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

            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, explosionRadius, meteorBuffer);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = meteorBuffer[i];
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