using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 파이로폴 화염구의 낙하, 폭발 애니메이션 및 타원형 피해 판정을 담당합니다.
    /// </summary>
    public sealed class PyrofallProjectile : MonoBehaviour
    {
        private enum State
        {
            Inactive,
            Falling,
            Exploding
        }

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] fireballFrames;
        [SerializeField] private Sprite[] explosionFrames;
        [SerializeField, Min(1f)] private float fallAnimationFps = 12f;
        [SerializeField, Min(1)] private int fallingLoopFrameCount = 6;
        [SerializeField, Min(1f)] private float explosionAnimationFps = 12f;
        [SerializeField] private int sortingOrder = 15;
        [Tooltip("조준점 기준 실제 낙하 및 폭발 위치의 Y축 오프셋입니다.")]
        [SerializeField] private float impactYOffset;

        [Header("Explosion Hit Area")]
        [SerializeField] private LayerMask enemyLayers;
        [SerializeField, Min(1)] private int overlapBufferSize = 64;
        [Tooltip("64px 프레임 중 폭발 그림이 차지하는 가로 비율입니다. 현재 이미지의 알파 영역은 60px입니다.")]
        [SerializeField, Range(0.01f, 1f)] private float explosionContentWidthFraction = 0.9375f;
        [Tooltip("폭발 그림의 알파 영역(60x31px)에 맞춘 세로/가로 반지름 비율입니다.")]
        [SerializeField, Range(0.01f, 1f)] private float explosionEllipseAspect = 0.5166667f;

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        private Collider2D[] overlapBuffer;
        private State state;
        private Vector3 targetPosition;
        private float damage;
        private float attackRange;
        private float fallSpeed;
        private float animationTime;
        private string poolTag;
        private GameObject hitEffectPrefab;
        private PyrofallWeapon sourceWeapon;
        private Vector2 ellipseCenter;
        private Vector2 ellipseRadii;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            EnsureOverlapBuffer();
        }

        private void OnEnable()
        {
            state = State.Inactive;
            animationTime = 0f;
            transform.localScale = Vector3.one;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }
        }

        public void Initialize(
            Vector3 destination,
            float impactDamage,
            float range,
            float speed,
            string projectilePoolTag,
            GameObject impactHitEffectPrefab,
            PyrofallWeapon owner)
        {
            targetPosition = destination + Vector3.up * impactYOffset;
            damage = Mathf.Max(0f, impactDamage);
            attackRange = Mathf.Max(0.1f, range);
            fallSpeed = Mathf.Max(0.1f, speed);
            poolTag = projectilePoolTag;
            hitEffectPrefab = impactHitEffectPrefab;
            sourceWeapon = owner;
            animationTime = 0f;
            state = State.Falling;

            transform.localScale = Vector3.one;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.sortingOrder = sortingOrder;
                spriteRenderer.sprite = GetFrame(fireballFrames, 0);
            }
        }

        private void Update()
        {
            switch (state)
            {
                case State.Falling:
                    AdvanceFalling();
                    break;
                case State.Exploding:
                    AdvanceExplosion();
                    break;
            }
        }

        private void AdvanceFalling()
        {
            animationTime += Time.deltaTime;
            int loopCount = Mathf.Min(
                Mathf.Max(1, fallingLoopFrameCount),
                fireballFrames != null ? fireballFrames.Length : 0);
            if (loopCount > 0 && spriteRenderer != null)
            {
                int frameIndex = Mathf.FloorToInt(animationTime * fallAnimationFps) % loopCount;
                spriteRenderer.sprite = fireballFrames[frameIndex];
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                fallSpeed * Time.deltaTime);

            if ((transform.position - targetPosition).sqrMagnitude <= 0.0001f)
            {
                BeginExplosion();
            }
        }

        private void BeginExplosion()
        {
            state = State.Exploding;
            animationTime = 0f;
            transform.position = targetPosition;

            Sprite firstExplosionFrame = GetFrame(explosionFrames, 0);
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = firstExplosionFrame;
            }

            ResizeExplosion(firstExplosionFrame);
            DealExplosionDamage();

            if (explosionFrames == null || explosionFrames.Length == 0)
            {
                ReturnToPool();
            }
        }

        private void ResizeExplosion(Sprite explosionSprite)
        {
            float nativeWidth = explosionSprite != null ? explosionSprite.bounds.size.x : 2f;
            float nativeContentRadius = Mathf.Max(
                0.01f,
                nativeWidth * explosionContentWidthFraction * 0.5f);
            float uniformScale = attackRange / nativeContentRadius;
            transform.localScale = new Vector3(uniformScale, uniformScale, 1f);

            ellipseRadii = new Vector2(attackRange, attackRange * explosionEllipseAspect);
            ellipseCenter = (Vector2)targetPosition + Vector2.up * ellipseRadii.y;
        }

        private void DealExplosionDamage()
        {
            EnsureOverlapBuffer();
            damagedTargets.Clear();

            int hitCount = Physics2D.OverlapBoxNonAlloc(
                ellipseCenter,
                ellipseRadii * 2f,
                0f,
                overlapBuffer,
                enemyLayers);

            float inverseX = 1f / Mathf.Max(0.01f, ellipseRadii.x);
            float inverseY = 1f / Mathf.Max(0.01f, ellipseRadii.y);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = overlapBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                Vector2 closestPoint = hit.ClosestPoint(ellipseCenter);
                Vector2 normalizedOffset = new Vector2(
                    (closestPoint.x - ellipseCenter.x) * inverseX,
                    (closestPoint.y - ellipseCenter.y) * inverseY);
                if (normalizedOffset.sqrMagnitude > 1f)
                {
                    continue;
                }

                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !damagedTargets.Add(target))
                {
                    continue;
                }

                MonoBehaviour targetBehaviour = target as MonoBehaviour;
                if (targetBehaviour == null || !targetBehaviour.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float targetZ = targetBehaviour.transform.position.z;
                target.TakeDamage(damage);
                sourceWeapon?.ApplyStatusEffects(target);
                PlayHitEffect(closestPoint, targetZ);
            }
        }

        private void PlayHitEffect(Vector2 hitPoint, float targetZ)
        {
            if (hitEffectPrefab == null)
            {
                return;
            }

            Vector3 position = new Vector3(hitPoint.x, hitPoint.y, targetZ);
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            GameObject effect = pool != null
                ? pool.SpawnFromPool(hitEffectPrefab, position, Quaternion.identity)
                : Instantiate(hitEffectPrefab, position, Quaternion.identity);
            if (effect == null)
            {
                return;
            }

            if (effect.TryGetComponent(out PyrofallHitEffect hitEffect))
            {
                hitEffect.Initialize(hitEffectPrefab.name, pool != null);
            }
            else if (pool != null)
            {
                pool.ReturnToPool(hitEffectPrefab.name, effect);
            }
            else
            {
                Destroy(effect);
            }
        }

        private void AdvanceExplosion()
        {
            animationTime += Time.deltaTime;
            int frameIndex = Mathf.FloorToInt(animationTime * explosionAnimationFps);
            if (explosionFrames == null || frameIndex >= explosionFrames.Length)
            {
                ReturnToPool();
                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = explosionFrames[frameIndex];
            }
        }

        private void EnsureOverlapBuffer()
        {
            int size = Mathf.Max(1, overlapBufferSize);
            if (overlapBuffer == null || overlapBuffer.Length != size)
            {
                overlapBuffer = new Collider2D[size];
            }
        }

        private void ReturnToPool()
        {
            state = State.Inactive;
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private static Sprite GetFrame(Sprite[] frames, int index)
        {
            return frames != null && index >= 0 && index < frames.Length ? frames[index] : null;
        }

        private void OnDisable()
        {
            state = State.Inactive;
            damagedTargets.Clear();
            hitEffectPrefab = null;
            sourceWeapon = null;
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 center = Application.isPlaying
                ? ellipseCenter
                : (Vector2)transform.position + Vector2.up * explosionEllipseAspect;
            Vector2 radii = Application.isPlaying && ellipseRadii.sqrMagnitude > 0f
                ? ellipseRadii
                : new Vector2(1f, explosionEllipseAspect);

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(radii.x, radii.y, 1f));
            Gizmos.color = new Color(1f, 0.25f, 0.05f, 0.8f);
            Gizmos.DrawWireSphere(Vector3.zero, 1f);
            Gizmos.matrix = previousMatrix;
        }
    }
}
