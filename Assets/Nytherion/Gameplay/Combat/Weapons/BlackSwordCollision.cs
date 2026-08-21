using System;
using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    /// <summary>
    /// BlackSword 검기의 시각 효과, 콜라이더 타격 판정과 풀 수명을 관리합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BlackSwordCollision : MonoBehaviour
    {
        [Header("Slash Visual Settings")]
        [SerializeField] private SpriteRenderer coreRenderer;
        [SerializeField] private SpriteRenderer slashSpriteRenderer;
        [Tooltip("64x64 검기 스프라이트 애니메이션의 표시 크기")]
        [SerializeField, Min(0.1f)] private float slashSpriteScale = 2f;
        [Tooltip("스프라이트 검기가 완전히 선명하게 유지되는 시간")]
        [SerializeField, Min(0f)] private float slashSpriteHoldDuration = 0.42f;
        [Tooltip("스프라이트 검기가 서서히 사라지는 시간")]
        [SerializeField, Min(0f)] private float slashSpriteFadeDuration = 0f;

        [Header("Hitbox Settings")]
        [SerializeField] private Collider2D hitboxCollider;

        [Header("Lifecycle Settings")]
        [Tooltip("애니메이션 이벤트가 실패할 경우를 대비한 최대 생존 시간")]
        [SerializeField] private float maxLifetime = 2f;
        [SerializeField] private string poolTag = "BlackSword_Slash_Effect";

        private Coroutine safetyReturnCoroutine;
        private Coroutine visualReturnCoroutine;
        private Vector3 baseScale;
        private float visualStartTime;
        private bool isVisualConfigured;
        private Color activeSlashSpriteColor = Color.white;
        private Vector3 baseHitboxLocalScale = Vector3.one;
        private LayerMask targetLayerMask;
        private Action<Collider2D, IDamageable> hitCallback;
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private Transform followTarget;
        private Vector3 followWorldOffset;

        private float TotalSlashSpriteLifetime => slashSpriteHoldDuration + slashSpriteFadeDuration;
        private float TotalVisualLifetime => TotalSlashSpriteLifetime;

        private void Awake()
        {
            poolTag = gameObject.name.Replace("(Clone)", string.Empty).Trim();
            coreRenderer ??= GetComponent<SpriteRenderer>();
            hitboxCollider ??= GetComponentInChildren<Collider2D>(true);
            EnsureRigidbody();
            baseScale = transform.localScale;
            if (hitboxCollider != null)
            {
                hitboxCollider.isTrigger = true;
                baseHitboxLocalScale = hitboxCollider.transform.localScale;
            }

            EnsureSlashSpriteRenderer();
        }

        private void OnEnable()
        {
            transform.localScale = baseScale;
            isVisualConfigured = false;
            followTarget = null;
            followWorldOffset = Vector3.zero;
            hitCallback = null;
            hitTargets.Clear();
            ResetHitboxTransform();
            DisableHitbox();

            if (coreRenderer != null)
            {
                coreRenderer.flipY = false;
                coreRenderer.enabled = false;
                coreRenderer.color = Color.white;
            }

            if (slashSpriteRenderer != null)
            {
                slashSpriteRenderer.flipY = false;
                slashSpriteRenderer.enabled = false;
                slashSpriteRenderer.color = Color.white;
            }

            if (safetyReturnCoroutine != null)
            {
                StopCoroutine(safetyReturnCoroutine);
            }
            if (visualReturnCoroutine != null)
            {
                StopCoroutine(visualReturnCoroutine);
                visualReturnCoroutine = null;
            }
            safetyReturnCoroutine = StartCoroutine(SafetyReturnRoutine());
        }

        private void LateUpdate()
        {
            UpdateFollowPosition();
            if (!isVisualConfigured) return;

            float elapsedTime = Time.time - visualStartTime;
            float slashSpriteAlpha = elapsedTime <= slashSpriteHoldDuration
                ? 1f
                : slashSpriteFadeDuration > 0f
                    ? 1f - Mathf.Clamp01(
                        (elapsedTime - slashSpriteHoldDuration) / slashSpriteFadeDuration)
                    : 0f;

            UpdateSlashSpriteRenderer(slashSpriteAlpha);

        }

        public void ConfigureVisual(
            int comboStep,
            float thirdSlashScale,
            float swingDirectionSign)
        {
            bool isContextReversed = swingDirectionSign < 0f;
            bool isReverseSwing = (comboStep == 1) ^ isContextReversed;
            bool isThirdSwing = comboStep == 2;

            transform.localScale = baseScale * (isThirdSwing ? thirdSlashScale : 1f);
            ApplyHitboxFlip(isReverseSwing);
            visualStartTime = Time.time;
            isVisualConfigured = true;

            if (coreRenderer != null)
            {
                coreRenderer.flipY = isReverseSwing;
                coreRenderer.enabled = false;
                activeSlashSpriteColor = isThirdSwing
                    ? new Color(1f, 0.82f, 1f, 1f)
                    : Color.white;
            }

            UpdateSlashSpriteRenderer(1f);

        }

        public void ConfigureFollowTarget(Transform target, Vector3 worldOffset)
        {
            followTarget = target;
            followWorldOffset = worldOffset;
            UpdateFollowPosition();
        }

        private void UpdateFollowPosition()
        {
            if (followTarget != null)
            {
                transform.position = followTarget.position + followWorldOffset;
            }
        }

        public void ConfigureHitbox(
            LayerMask targetLayers,
            Action<Collider2D, IDamageable> onHit)
        {
            targetLayerMask = targetLayers;
            hitCallback = onHit;
            hitTargets.Clear();
        }

        private void EnsureSlashSpriteRenderer()
        {
            if (coreRenderer == null) return;

            if (slashSpriteRenderer == null)
            {
                GameObject slashObject = new GameObject("AnimatedSlash");
                slashObject.transform.SetParent(transform, false);
                slashSpriteRenderer = slashObject.AddComponent<SpriteRenderer>();
            }

            slashSpriteRenderer.transform.localScale = Vector3.one * slashSpriteScale;
            slashSpriteRenderer.sprite = coreRenderer.sprite;
            slashSpriteRenderer.sharedMaterial = coreRenderer.sharedMaterial;
            slashSpriteRenderer.sortingLayerID = coreRenderer.sortingLayerID;
            slashSpriteRenderer.sortingOrder = coreRenderer.sortingOrder;
            coreRenderer.enabled = false;
        }

        private void UpdateSlashSpriteRenderer(float alpha)
        {
            if (coreRenderer == null || slashSpriteRenderer == null) return;

            slashSpriteRenderer.sprite = coreRenderer.sprite;
            slashSpriteRenderer.flipX = coreRenderer.flipX;
            slashSpriteRenderer.flipY = coreRenderer.flipY;
            slashSpriteRenderer.sortingLayerID = coreRenderer.sortingLayerID;
            slashSpriteRenderer.sortingOrder = coreRenderer.sortingOrder;

            Color spriteColor = activeSlashSpriteColor;
            spriteColor.a *= alpha;
            slashSpriteRenderer.color = spriteColor;
            slashSpriteRenderer.enabled = alpha > 0f;
        }

        private IEnumerator SafetyReturnRoutine()
        {
            yield return new WaitForSeconds(maxLifetime);
            ReturnToPool();
        }

        public void EnableHitbox()
        {
            hitTargets.Clear();
            if (hitboxCollider != null)
            {
                hitboxCollider.enabled = hitCallback != null;
            }
        }

        public void DisableHitbox()
        {
            if (hitboxCollider != null)
            {
                hitboxCollider.enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            ProcessHit(collision);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            ProcessHit(collision);
        }

        private void ProcessHit(Collider2D collision)
        {
            if (collision == null
                || hitCallback == null
                || hitboxCollider == null
                || !hitboxCollider.enabled)
            {
                return;
            }
            if ((targetLayerMask.value & (1 << collision.gameObject.layer)) == 0) return;

            IDamageable target = collision.GetComponent<IDamageable>();
            if (target == null)
            {
                target = collision.GetComponentInParent<IDamageable>();
            }

            if (target == null || !hitTargets.Add(target)) return;
            hitCallback.Invoke(collision, target);
        }

        private void EnsureRigidbody()
        {
            Rigidbody2D hitboxBody = GetComponent<Rigidbody2D>();
            if (hitboxBody == null)
            {
                hitboxBody = gameObject.AddComponent<Rigidbody2D>();
            }

            hitboxBody.bodyType = RigidbodyType2D.Kinematic;
            hitboxBody.gravityScale = 0f;
            hitboxBody.simulated = true;
        }

        private void ApplyHitboxFlip(bool flipY)
        {
            if (hitboxCollider == null) return;

            Vector3 hitboxScale = baseHitboxLocalScale;
            hitboxScale.y = Mathf.Abs(hitboxScale.y) * (flipY ? -1f : 1f);
            hitboxCollider.transform.localScale = hitboxScale;
        }

        private void ResetHitboxTransform()
        {
            if (hitboxCollider != null)
            {
                hitboxCollider.transform.localScale = baseHitboxLocalScale;
            }
        }

        public void OnAnimationEnd()
        {
            float remainingVisualTime = TotalVisualLifetime - (Time.time - visualStartTime);
            if (isVisualConfigured && remainingVisualTime > 0f)
            {
                if (visualReturnCoroutine != null)
                {
                    StopCoroutine(visualReturnCoroutine);
                }
                visualReturnCoroutine = StartCoroutine(ReturnAfterVisualRoutine(remainingVisualTime));
                return;
            }

            ReturnToPool();
        }

        private IEnumerator ReturnAfterVisualRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            visualReturnCoroutine = null;
            ReturnToPool();
        }

        public void ReturnToPool()
        {
            isVisualConfigured = false;
            followTarget = null;
            followWorldOffset = Vector3.zero;
            DisableHitbox();
            hitCallback = null;
            hitTargets.Clear();

            if (safetyReturnCoroutine != null)
            {
                StopCoroutine(safetyReturnCoroutine);
                safetyReturnCoroutine = null;
            }
            if (visualReturnCoroutine != null)
            {
                StopCoroutine(visualReturnCoroutine);
                visualReturnCoroutine = null;
            }

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
