using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 가장 가까운 적에게 달려가 충돌하면 범위 피해를 주고 폭발하는 일회성 로봇입니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class ExplosiveRobot : MonoBehaviour
    {
        [Header("추적")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 3.25f;
        [SerializeField, Min(0.05f)] private float contactDetonationRadius = 0.25f;
        [SerializeField, Min(0.1f)] private float targetSearchRange = 12f;
        [SerializeField, Min(0.1f)] private float lifetime = 5f;
        [SerializeField] private bool invertFacing;

        [Header("폭발")]
        [SerializeField, Min(0.1f)] private float explosionRadius = 0.8f;
        [SerializeField] private GameObject explosionVfxPrefab;
        [SerializeField, Min(0.01f)] private float explosionVfxScale = 0.75f;
        [SerializeField, Min(0.1f)] private float fallbackVfxLifetime = 0.6f;

        [Header("애니메이션")]
        [SerializeField] private string runningBoolParam = "IsRunning";

        private static readonly Collider2D[] TargetBuffer = new Collider2D[32];
        private static readonly Collider2D[] ContactBuffer = new Collider2D[8];

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private Transform summoner;
        private Transform target;
        private Rigidbody2D robotRigidbody;
        private CircleCollider2D robotCollider;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private float damage;
        private float expireTime;
        private bool isInitialized;
        private bool hasExploded;
        private ObjectPoolManager returnPool;
        private string poolTag;
        private bool isReturning;
        private bool originalFlipX;

        public void SetPool(ObjectPoolManager pool, string tag)
        {
            returnPool = pool;
            poolTag = tag;
        }

        public void Initialize(
            Transform sourceSummoner,
            Transform initialTarget,
            float explosionDamage,
            SpriteRenderer summonerRenderer)
        {
            summoner = sourceSummoner;
            target = initialTarget;
            damage = Mathf.Max(0f, explosionDamage);
            expireTime = Time.time + Mathf.Max(0.1f, lifetime);
            hasExploded = false;
            isReturning = false;

            CacheComponents();
            damagedTargets.Clear();
            robotRigidbody.simulated = true;
            robotRigidbody.velocity = Vector2.zero;
            robotRigidbody.angularVelocity = 0f;
            robotCollider.enabled = true;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.flipX = originalFlipX;
            }
            if (animator != null)
            {
                animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
            }
            SetRunningAnimation(false);
            if (summonerRenderer != null && spriteRenderer != null)
            {
                spriteRenderer.sortingLayerID = summonerRenderer.sortingLayerID;
                spriteRenderer.sortingOrder = summonerRenderer.sortingOrder + 1;
            }
            isInitialized = true;
        }

        private void Awake()
        {
            CacheComponents();
            originalFlipX = spriteRenderer != null && spriteRenderer.flipX;
            robotRigidbody.gravityScale = 0f;
            robotRigidbody.drag = 0f;
            robotRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            robotRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            robotCollider.isTrigger = true;
        }

        private void OnEnable()
        {
            isInitialized = false;
            isReturning = false;
            hasExploded = false;
            robotCollider.enabled = false;
        }

        private void OnDisable()
        {
            isInitialized = false;
            if (summoner != null && summoner.TryGetComponent(out BoomMaker boomMaker))
            {
                boomMaker.ReleaseExplosiveRobot(this);
            }
            summoner = null;
            target = null;
            damagedTargets.Clear();
            if (robotRigidbody != null)
            {
                robotRigidbody.velocity = Vector2.zero;
                robotRigidbody.angularVelocity = 0f;
            }
            if (robotCollider != null) robotCollider.enabled = false;
        }

        public void ReturnToPool()
        {
            if (isReturning || !gameObject.activeSelf) return;
            isReturning = true;
            isInitialized = false;
            if (returnPool != null && !string.IsNullOrEmpty(poolTag))
            {
                returnPool.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (!isInitialized || hasExploded)
            {
                return;
            }

            if (summoner == null || !summoner.gameObject.activeInHierarchy)
            {
                ReturnToPool();
                return;
            }

            if (Time.time >= expireTime)
            {
                Explode();
                return;
            }

            if (!IsValidTarget(target))
            {
                target = FindClosestEnemy();
            }

            bool isRunning = target != null;
            SetRunningAnimation(isRunning);
            UpdateFacing();
        }

        private void FixedUpdate()
        {
            if (!isInitialized || hasExploded || robotRigidbody == null)
            {
                return;
            }

            if (TryExplodeFromOverlap())
            {
                return;
            }

            if (!IsValidTarget(target))
            {
                robotRigidbody.velocity = Vector2.zero;
                return;
            }

            Vector2 targetOffset = (Vector2)target.position - robotRigidbody.position;
            if (targetOffset.sqrMagnitude <= contactDetonationRadius * contactDetonationRadius)
            {
                Explode();
                return;
            }

            Vector2 direction = targetOffset.normalized;
            robotRigidbody.velocity = direction * moveSpeed;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryExplodeFromCollider(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryExplodeFromCollider(other);
        }

        private void CacheComponents()
        {
            if (robotRigidbody == null)
            {
                robotRigidbody = GetComponent<Rigidbody2D>();
            }
            if (robotCollider == null)
            {
                robotCollider = GetComponent<CircleCollider2D>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private Transform FindClosestEnemy()
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, targetSearchRange, TargetBuffer);
            Transform closestTarget = null;
            float closestDistanceSqr = Mathf.Infinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = TargetBuffer[i];
                if (!TryResolveEnemy(hit, out _, out Transform enemyTransform))
                {
                    continue;
                }

                float distanceSqr = (enemyTransform.position - transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestTarget = enemyTransform;
                }
            }

            return closestTarget;
        }

        private static bool IsValidTarget(Transform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            IDamageable damageable = candidate.GetComponentInParent<IDamageable>();
            MonoBehaviour damageableBehaviour = damageable as MonoBehaviour;
            return damageableBehaviour != null && damageableBehaviour.isActiveAndEnabled &&
                   (candidate.CompareTag("Enemy") || damageableBehaviour.CompareTag("Enemy"));
        }

        private bool TryExplodeFromOverlap()
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(
                robotRigidbody.position,
                contactDetonationRadius,
                ContactBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                if (TryResolveEnemy(ContactBuffer[i], out _, out _))
                {
                    Explode();
                    return true;
                }
            }

            return false;
        }

        private void TryExplodeFromCollider(Collider2D other)
        {
            if (isInitialized && !hasExploded && TryResolveEnemy(other, out _, out _))
            {
                Explode();
            }
        }

        private static bool TryResolveEnemy(
            Collider2D hit,
            out IDamageable damageable,
            out Transform enemyTransform)
        {
            damageable = hit != null ? hit.GetComponentInParent<IDamageable>() : null;
            MonoBehaviour damageableBehaviour = damageable as MonoBehaviour;
            bool isEnemyBodyCollider = hit != null &&
                                       (!hit.isTrigger || hit.transform == damageableBehaviour?.transform);
            if (hit == null || damageableBehaviour == null || !damageableBehaviour.isActiveAndEnabled ||
                !isEnemyBodyCollider ||
                (!hit.CompareTag("Enemy") && !damageableBehaviour.CompareTag("Enemy")))
            {
                damageable = null;
                enemyTransform = null;
                return false;
            }

            enemyTransform = damageableBehaviour.transform;
            return true;
        }

        private void UpdateFacing()
        {
            if (target == null || spriteRenderer == null)
            {
                return;
            }

            float horizontalDifference = target.position.x - transform.position.x;
            if (Mathf.Abs(horizontalDifference) <= 0.01f)
            {
                return;
            }

            bool facesLeft = horizontalDifference < 0f;
            spriteRenderer.flipX = invertFacing ? !facesLeft : facesLeft;
        }

        private void SetRunningAnimation(bool isRunning)
        {
            if (animator == null || string.IsNullOrEmpty(runningBoolParam))
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == runningBoolParam && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(runningBoolParam, isRunning);
                    return;
                }
            }
        }

        private void Explode()
        {
            if (!isInitialized || hasExploded)
            {
                return;
            }

            hasExploded = true;
            if (robotRigidbody != null)
            {
                robotRigidbody.velocity = Vector2.zero;
            }
            if (robotCollider != null)
            {
                robotCollider.enabled = false;
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }

            ApplyExplosionDamage();
            SpawnExplosionVisual();
            ReturnToPool();
        }

        private void ApplyExplosionDamage()
        {
            damagedTargets.Clear();
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, explosionRadius, TargetBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = TargetBuffer[i];
                if (!TryResolveEnemy(hit, out IDamageable damageable, out _) ||
                    !damagedTargets.Add(damageable))
                {
                    continue;
                }

                damageable.TakeDamage(damage);
            }
        }

        private void SpawnExplosionVisual()
        {
            if (explosionVfxPrefab == null)
            {
                return;
            }

            GameObject explosionObject;
            if (ObjectPoolManager.Instance != null)
            {
                explosionObject = ObjectPoolManager.Instance.SpawnFromPool(
                    explosionVfxPrefab,
                    transform.position,
                    Quaternion.identity,
                    5);
            }
            else
            {
                explosionObject = Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
                Destroy(explosionObject, fallbackVfxLifetime);
            }

            if (explosionObject != null)
            {
                explosionObject.transform.localScale = Vector3.one * explosionVfxScale;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
            Gizmos.color = new Color(1f, 0.9f, 0.15f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, contactDetonationRadius);
        }
    }
}
