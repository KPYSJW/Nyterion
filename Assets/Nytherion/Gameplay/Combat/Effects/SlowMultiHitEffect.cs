using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using UnityEditor.VersionControl;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(CollisionObject))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class SlowMultiHitEffect : MonoBehaviour, IProjectileEffect
    {
        public float slowSpeed = 2f;
        public float tickRate = 0.5f;
        public float lifeTime = 5f;

        private Rigidbody2D rb;
        private CollisionObject collisionObject;

        private float lifeTimer;
        private float tickTimer;
        private bool hasHit = false;

        private HashSet<IDamageable> targetsInRange = new HashSet<IDamageable>();
        private List<IDamageable> removeList = new List<IDamageable>();
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            collisionObject = GetComponent<CollisionObject>();
        }

        private void OnEnable()
        {
            hasHit = false;
            targetsInRange.Clear();
            removeList.Clear();
            lifeTimer = lifeTime;
        }

        private void Update()
        {
            if (hasHit)
            {
                lifeTimer -= Time.deltaTime;
                if (lifeTimer <= 0)
                {
                    collisionObject.ReturnToPool();
                    return;
                }

                tickTimer -= Time.deltaTime;
                if (tickTimer <= 0)
                {
                    tickTimer = tickRate;
                    DealTickDamage();
                }
            }
        }

        private void DealTickDamage()
        {
            removeList.Clear();

            foreach (var target in targetsInRange)
            {
                if (target != null && target is MonoBehaviour mb && mb.gameObject.activeInHierarchy)
                {
                    target.TakeDamage(collisionObject.damage);
                }
                else
                {
                    removeList.Add(target);
                }
            }

            foreach (var t in removeList)
            {
                targetsInRange.Remove(t);
            }
        }
        public bool OnHit(Collider2D targetCollider)
        {
            if (targetCollider.CompareTag("Enemy"))
            {
                var target = targetCollider.GetComponent<IDamageable>();
                if (target != null)
                {
                    if (!hasHit)
                    {
                        hasHit = true;
                        rb.velocity = rb.velocity.normalized * slowSpeed;
                        tickTimer = tickRate;
                    }

                    targetsInRange.Add(target);
                }

                return true;
            }
            return false;
        }
        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Enemy"))
            {
                var target = collision.GetComponent<IDamageable>();
                if (target != null && targetsInRange.Contains(target))
                {
                    targetsInRange.Remove(target);
                }
            }
        }
    }
}