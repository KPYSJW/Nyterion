using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class CollisionObject : MonoBehaviour
    {
        [HideInInspector] public float damage;

        [Header("Pool Settings")]
        public string poolTag = "PlayerProjectile";

        private IProjectileEffect[] effects;

        private void Awake()
        {
            effects = GetComponents<IProjectileEffect>();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Enemy"))
            {
                var target = collision.GetComponent<IDamageable>();
                target?.TakeDamage(damage);

                bool shouldSurvive = false;
                foreach (var effect in effects)
                {
                    if (effect is MonoBehaviour mb && !mb.enabled) continue;

                    if (effect.OnHit(collision))
                    {
                        shouldSurvive = true;
                    }
                }

                if (!shouldSurvive)
                {
                    ReturnToPool();
                }
            }
            else if (collision.CompareTag("Wall"))
            {
                ReturnToPool();
            }
        }

        public void ReturnToPool()
        {
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        //private void OnBecameInvisible()
        //{
        //    ReturnToPool();
        //}
    }
}