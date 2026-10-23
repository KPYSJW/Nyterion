using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(CollisionObject))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class SlowMultiHitModifier : MonoBehaviour, IProjModifier
    {
        public float slowSpeed = 2f;
        public float tickRate = 0.5f;
        public float lifeTime = 5f;
        [Tooltip("반복 타격에도 투사체의 피격 효과를 표시합니다.")]
        public bool playHitEffectOnTick;

        private Rigidbody2D rb;
        private CollisionObject collisionObject;

        private float lifeTimer;
        private float tickTimer;
        private bool hasHit = false;

        private Dictionary<IDamageable, Collider2D> targetsInRange = new Dictionary<IDamageable, Collider2D>();
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
            tickTimer = tickRate;
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

            foreach (var entry in targetsInRange)
            {
                IDamageable target = entry.Key;
                Collider2D targetCollider = entry.Value;
                if (target != null && target is MonoBehaviour mb && mb.gameObject.activeInHierarchy &&
                    targetCollider != null && targetCollider.isActiveAndEnabled)
                {
                    // 처치로 적이 비활성화되기 전에 현재 탄환과의 접촉 위치를 보관합니다.
                    Vector2 hitPoint = targetCollider.ClosestPoint(transform.position);
                    target.TakeDamage(collisionObject.damage, collisionObject.isChainDamage);
                    if (playHitEffectOnTick)
                    {
                        WeaponVFXHelper.PlayHitEffect(collisionObject.hitEffectPrefab, hitPoint, collisionObject.chargePercent);
                    }
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
                var target = targetCollider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    if (!hasHit)
                    {
                        hasHit = true;
                        rb.velocity = rb.velocity.normalized * slowSpeed;
                        tickTimer = tickRate;
                    }

                    targetsInRange[target] = targetCollider;
                }

                return true;
            }
            return false;
        }
        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Enemy"))
            {
                var target = collision.GetComponentInParent<IDamageable>();
                if (target != null && targetsInRange.ContainsKey(target))
                {
                    targetsInRange.Remove(target);
                }
            }
        }
    }
}
