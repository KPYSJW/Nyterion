using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public class CollisionObject : MonoBehaviour
    {
        [HideInInspector] public float damage;
        [HideInInspector] public bool isPiercing = false;

        [Header("Pool Settings")]
        [Tooltip("ObjectPoolManager에 등록된 이 투사체의 풀 태그 (예: 'PlayerProjectile')")]
        public string poolTag = "PlayerProjectile";

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Enemy"))
            {
                IDamageable target = collision.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(damage);
                }

                if (!isPiercing)
                {
                    DisableAndReturnToPool();
                }
            }
            else if (collision.CompareTag("Wall"))
            {
                DisableAndReturnToPool();
            }
        }
        private void DisableAndReturnToPool()
        {
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
        private void OnBecameInvisible()
        {
            DisableAndReturnToPool();
        }
    }
}

