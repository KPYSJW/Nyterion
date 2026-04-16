using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public class ExplosionEffect : MonoBehaviour, IProjectileEffect
    {
        [Header("Explosion Settings")]
        public float explosionDamageMultiplier = 1f;
        public string explosionVisualPoolTag = "";

        private CollisionObject col;

        private void Awake()
        {
            col = GetComponent<CollisionObject>();
        }

        public bool OnHit(Collider2D target)
        {
            float baseDamage = (col != null && col.damage > 0) ? col.damage : 10f;
            float finalExplosionDamage = baseDamage * explosionDamageMultiplier;

            if (!string.IsNullOrEmpty(explosionVisualPoolTag) && ObjectPoolManager.Instance != null)
            {
                GameObject explosionVisual = ObjectPoolManager.Instance.SpawnFromPool(explosionVisualPoolTag, transform.position, Quaternion.identity);

                if (explosionVisual.TryGetComponent<Effects.ExplosionDamage>(out var expDamage))
                {
                    expDamage.Initialize(finalExplosionDamage);
                }
            }

            return false;
        }

    }
}