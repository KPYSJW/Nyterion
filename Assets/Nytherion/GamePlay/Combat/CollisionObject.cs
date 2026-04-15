using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Combat
{
    public class CollisionObject : MonoBehaviour
    {
        [HideInInspector] public float damage;

        [Header("Pool Settings")]
        public string poolTag = "PlayerProjectile";

        private IProjectileEffect[] effects;

        private ObjectPoolManager poolManager;

        [Inject]
        public void Construct(ObjectPoolManager poolManager)
        {
            this.poolManager = poolManager;
        }

        private void Awake()
        {
            effects = GetComponents<IProjectileEffect>();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            bool isEnemy = collision.CompareTag("Enemy");
            bool isWall = collision.CompareTag("Wall");

            if (isEnemy || isWall)
            {
                if (isEnemy)
                {
                    var target = collision.GetComponent<IDamageable>();
                    target?.TakeDamage(damage);
                }

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
        }

        public void ReturnToPool()
        {
            if (poolManager!= null && !string.IsNullOrEmpty(poolTag))
            {
                poolManager.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}