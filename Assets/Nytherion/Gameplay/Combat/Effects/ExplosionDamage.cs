using UnityEngine;
using Nytherion.Core.Interfaces; 

namespace Nytherion.GamePlay.Combat.Effects
{
    public class ExplosionDamage : MonoBehaviour
    {
        private float explosionDamage;

        public void Initialize(float damage)
        {
            explosionDamage = damage;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Enemy"))
            {
                if (collision.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(explosionDamage);
                }
            }
        }
    }
}