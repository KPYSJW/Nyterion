using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class BounceModifier : MonoBehaviour, IProjModifier
    {
        public int maxBounces = 3;
        public float bounceRadius = 5f;

        private int currentBounces;
        private float searchRadius;
        private bool randomWhenNoTarget;
        private bool enemyHitsOnly;
        private HashSet<Transform> hitTargets = new HashSet<Transform>();
        private static readonly Collider2D[] bounceBuffer = new Collider2D[10];
        private readonly List<Collider2D> enemyHits = new List<Collider2D>(16);

        private void OnEnable()
        {
            currentBounces = maxBounces;
            hitTargets.Clear();
            searchRadius = bounceRadius;
            randomWhenNoTarget = enemyHitsOnly = false;
        }

        /// <summary>생성 또는 풀 재사용 직후에 루티 전용 옵션을 지정합니다. 기본 무기 튕김은 유지합니다.</summary>
        public void Configure(int bounces, float radius, bool randomFallback, bool onlyEnemies)
        {
            currentBounces = Mathf.Max(0, bounces);
            searchRadius = Mathf.Max(0.1f, radius);
            randomWhenNoTarget = randomFallback;
            enemyHitsOnly = onlyEnemies;
            hitTargets.Clear();
        }

        public bool ShouldIgnoreHit(Collider2D target) => enemyHitsOnly && hitTargets.Contains(ResolveTarget(target));

        private static Transform ResolveTarget(Collider2D target)
        {
            IDamageable damageable = target.GetComponentInParent<IDamageable>();
            return damageable is MonoBehaviour behaviour ? behaviour.transform : target.transform;
        }

        public bool OnHit(Collider2D target)
        {
            if (enemyHitsOnly && !CollisionObject.IsEnemyCollider(target)) return false;
            // 마지막 충돌도 기록하여 한 적의 여러 충돌체가 중복 피해를 만들지 않게 합니다.
            if (enemyHitsOnly) hitTargets.Add(ResolveTarget(target));
            if (currentBounces <= 0) return false;

            Transform hitTarget = enemyHitsOnly ? ResolveTarget(target) : target.transform;
            hitTargets.Add(hitTarget);

            enemyHits.Clear();
            int hitCount = enemyHitsOnly
                ? Physics2D.OverlapCircle(transform.position, searchRadius, new ContactFilter2D { useTriggers = true }, enemyHits)
                : Physics2D.OverlapCircleNonAlloc(transform.position, bounceRadius, bounceBuffer);
            Transform nextTarget = null;
            float closestDistSqr = Mathf.Infinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = enemyHitsOnly ? enemyHits[i] : bounceBuffer[i];
                if (!CollisionObject.IsEnemyCollider(hit)) continue;
                Transform candidate = enemyHitsOnly ? ResolveTarget(hit) : hit.transform;
                if (enemyHitsOnly)
                {
                    IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                    if (!(damageable is MonoBehaviour behaviour) || !behaviour.isActiveAndEnabled ||
                        (behaviour is EnemyBase enemy && enemy.isDead) ||
                        (hit.isTrigger && hit.transform != candidate)) continue;
                }
                if (candidate != hitTarget && !hitTargets.Contains(candidate))
                {
                    float distSqr = (transform.position - hit.transform.position).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        nextTarget = candidate;
                    }
                }
            }

            if (nextTarget != null || randomWhenNoTarget)
            {
                currentBounces--;
                Vector2 direction;
                if (nextTarget != null) direction = (nextTarget.position - transform.position).normalized;
                else
                {
                    float randomAngle = Random.Range(0f, Mathf.PI * 2f);
                    direction = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
                }

                if (TryGetComponent<Rigidbody2D>(out var rb))
                {
                    float currentSpeed = rb.velocity.magnitude;
                    rb.velocity = direction * currentSpeed;
                }

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                if (enemyHitsOnly && TryGetComponent(out ProjDistanceLimit distanceLimit) && distanceLimit.enabled)
                    distanceLimit.Initialize(searchRadius);

                return true; 
            }

            return false; 
        }
    }
}
