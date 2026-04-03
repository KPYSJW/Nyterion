using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public class ExplosionEffect : MonoBehaviour, IProjectileEffect
    {
        [Header("Explosion Settings")]
        public float explosionRadius = 3f;
        public float explosionDamageMultiplier = 1f;
        public string explosionVisualPoolTag = "";

        private CollisionObject col;

        private void Awake()
        {
            col = GetComponent<CollisionObject>();
        }

        public bool OnHit(Collider2D target)
        {
            if (!string.IsNullOrEmpty(explosionVisualPoolTag) && ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.SpawnFromPool(explosionVisualPoolTag, transform.position, Quaternion.identity);
            }

            float baseDamage = (col != null && col.damage > 0) ? col.damage : 10f;
            float finalExplosionDamage = baseDamage * explosionDamageMultiplier;

            ContactFilter2D filter = new ContactFilter2D();
            filter.NoFilter();
            filter.useTriggers = true; 

            List<Collider2D> hits = new List<Collider2D>();
            Physics2D.OverlapCircle(transform.position, explosionRadius, filter, hits);

            HashSet<IDamageable> damagedEnemies = new HashSet<IDamageable>();

            foreach (var hit in hits)
            {
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable != null)
                {
                    MonoBehaviour damageableMB = damageable as MonoBehaviour;
                    bool isEnemy = hit.CompareTag("Enemy") || (damageableMB != null && damageableMB.gameObject.CompareTag("Enemy"));

                    if (isEnemy && !damagedEnemies.Contains(damageable))
                    {
                        damageable.TakeDamage(finalExplosionDamage);
                        damagedEnemies.Add(damageable);

                        Debug.Log($"<color=green> 쾅! 주변 광역 데미지: [{damageableMB.gameObject.name}]에게 {finalExplosionDamage} 피해!</color>");
                    }
                }
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}