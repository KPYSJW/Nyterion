using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Enemy;
using System;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
public class SoulEaterProj : MonoBehaviour
{
    private float damage;
    private float speed;
    private float range;
    private string poolTag;
    private Vector3 startPosition;
    private bool isInitialized = false;
    private Action onKillCallback;

    public void Initialize(float damage, float speed, float range, string poolTag, Action onKillCallback = null)
    {
        this.damage = damage;
        this.speed = speed;
        this.range = range;
        this.poolTag = poolTag;
        this.onKillCallback = onKillCallback;
        startPosition = transform.position;
        isInitialized = true; 
    }

    void Update()
    {
        if (!isInitialized) return;

        transform.Translate(Vector3.right * (speed * Time.deltaTime));

        if ((transform.position - startPosition).sqrMagnitude >= range * range)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Weapon"))
        {
            return;
        }

        bool hitValidTarget = false;

        if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            bool wasDead = false;
            if (enemy != null)
            {
                wasDead = enemy.isDead;
            }

            damageable.TakeDamage(damage);

            if (enemy != null && !wasDead && enemy.isDead)
            {
                if (onKillCallback != null)
                {
                    onKillCallback.Invoke();
                }
            }
            hitValidTarget = true;
        }
        else if (other.CompareTag("Wall"))
        {
            hitValidTarget = true;
        }

        if (hitValidTarget)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        isInitialized = false;
        if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
        {
            ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
        }
        else
        {
            Destroy(gameObject); 
        }
    }
}
}
