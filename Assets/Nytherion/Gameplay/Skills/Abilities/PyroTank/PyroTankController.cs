using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>표적을 유지하며 돌진하고, 적·벽 접촉 또는 수명 만료 시 한 번 폭발합니다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class PyroTankController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer tankRenderer;
        [SerializeField] private Animator tankAnimator;
        [SerializeField] private GameObject explosionVisual;
        [SerializeField] private Animator explosionAnimator;
        [SerializeField] private Transform jumpVisual;
        [SerializeField] private SpriteRenderer jumpRenderer;
        [SerializeField] private Animator jumpAnimator;

        private static readonly int IsMoving = Animator.StringToHash("IsMoving");
        private readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        private readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>(16);
        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private readonly List<IDamageable> damageTargets = new List<IDamageable>();
        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private PyroTankSkillData data;
        private Collider2D targetCollider;
        private Transform target;
        private ContactFilter2D enemyFilter;
        private float expiresAt;
        private float explosionEndsAt;
        private bool hasExploded;
        private bool isJumping;
        private Vector2 jumpDirection;
        private Vector2 jumpStartPosition;
        private float jumpStartedAt;
        private float jumpProgress;
        private float effectSizeMultiplier = 1f;
        private Vector3 baseScale;
        private bool hasBaseScale;

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            // 비활성 프리팹은 Begin이 Awake보다 먼저 호출될 수 있습니다.
            if (!hasBaseScale)
            {
                baseScale = transform.localScale;
                hasBaseScale = true;
            }
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<CircleCollider2D>();
        }

        public void Begin(PyroTankSkillData skill, Vector3 position)
        {
            Begin(skill, position, Vector2.zero);
        }

        public void Begin(PyroTankSkillData skill, Vector3 position, Vector2 launchDirection,
            float sizeMultiplier = 1f)
        {
            CacheComponents();
            effectSizeMultiplier = Mathf.Max(0.01f, sizeMultiplier);
            transform.localScale = baseScale * effectSizeMultiplier;
            data = skill;
            hasExploded = false;
            target = null;
            targetCollider = null;
            expiresAt = Time.time + Mathf.Max(0.01f, data.lifetime);
            explosionEndsAt = 0f;
            body.simulated = false;
            body.velocity = Vector2.zero;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            body.position = position;
            bodyCollider.radius = Mathf.Max(0.01f, data.collisionRadius);
            bodyCollider.offset = Vector2.zero;
            bodyCollider.enabled = true;
            tankRenderer.enabled = true;
            tankRenderer.flipX = data.invertFacing;
            tankAnimator.enabled = true;
            explosionVisual.SetActive(false);
            ResetJumpVisual();
            enemyFilter = new ContactFilter2D { useTriggers = true };
            enemyFilter.SetLayerMask(data.enemyLayers);
            gameObject.SetActive(true);
            body.simulated = true;
            tankAnimator.Rebind();
            tankAnimator.SetBool(IsMoving, false);
            tankAnimator.Play("Idle", 0, 0f);
            if (launchDirection.sqrMagnitude > 0.0001f && data.jumpDuration > 0f && data.jumpAnimation != null &&
                jumpVisual != null && jumpRenderer != null && jumpAnimator != null)
            {
                isJumping = true;
                jumpDirection = launchDirection.normalized;
                jumpStartPosition = body.position;
                jumpStartedAt = Time.time;
                SetFacing(jumpDirection.x);
                tankAnimator.enabled = false;
                tankRenderer.enabled = false;
                jumpRenderer.flipX = tankRenderer.flipX;
                jumpVisual.gameObject.SetActive(true);
                jumpAnimator.Rebind();
                jumpAnimator.speed = 0f;
                UpdateJumpVisual();
            }
        }

        private void Update()
        {
            if (data == null) return;
            if (hasExploded)
            {
                return;
            }
            if (Time.time >= expiresAt) { Explode(); return; }
        }

        private void LateUpdate()
        {
            if (isJumping && !hasExploded) UpdateJumpVisual();
        }

        private void UpdateJumpVisual()
        {
            jumpAnimator.Play("Jump", 0, jumpProgress);
            jumpAnimator.Update(0f);
            float heightRatio;
            if (jumpProgress < 0.25f)
                heightRatio = Mathf.Sin(jumpProgress / 0.25f * Mathf.PI * 0.5f);
            else if (jumpProgress < 0.5f)
                heightRatio = 1f;
            else if (jumpProgress < 0.75f)
                heightRatio = Mathf.Cos((jumpProgress - 0.5f) / 0.25f * Mathf.PI * 0.5f);
            else
                heightRatio = 0f;
            float height = heightRatio * Mathf.Max(0f, data.jumpHeight);
            jumpVisual.localPosition = Vector3.up * (height / Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y)));
        }

        private void FixedUpdate()
        {
            if (data == null || hasExploded) return;
            if (Time.time >= expiresAt)
            {
                Explode();
                return;
            }

            ContactFilter2D contactFilter = new ContactFilter2D { useTriggers = true };
            overlaps.Clear();
            bodyCollider.OverlapCollider(contactFilter, overlaps);
            foreach (Collider2D hit in overlaps)
            {
                if (!IsDetonationContact(hit)) continue;
                Explode();
                return;
            }

            if (isJumping)
            {
                MoveJump();
                return;
            }

            if (!IsValidTarget()) FindClosestEnemy();
            if (target == null)
            {
                body.velocity = Vector2.zero;
                tankAnimator.SetBool(IsMoving, false);
                return;
            }

            Vector2 offset = (Vector2)target.position - body.position;
            Vector2 direction = offset.normalized;
            float distance = Mathf.Min(offset.magnitude, Mathf.Max(0.01f, data.moveSpeed) * Time.fixedDeltaTime);
            if (TryExplodeAlongPath(direction, distance)) return;

            body.velocity = direction * (distance / Time.fixedDeltaTime);
            tankAnimator.SetBool(IsMoving, distance > 0f);
            SetFacing(direction.x);
        }

        private bool TryExplodeAlongPath(Vector2 direction, float distance)
        {
            ContactFilter2D contactFilter = new ContactFilter2D { useTriggers = true };
            castHits.Clear();
            body.Cast(direction, contactFilter, castHits, distance);
            RaycastHit2D nearestHit = default;
            float nearestDistance = Mathf.Infinity;
            foreach (RaycastHit2D hit in castHits)
            {
                if (hit.distance >= nearestDistance || !IsDetonationContact(hit.collider)) continue;
                nearestHit = hit;
                nearestDistance = hit.distance;
            }
            if (nearestHit.collider != null)
            {
                body.position += direction * Mathf.Max(0f, nearestDistance - 0.001f);
                Explode();
                return true;
            }
            return false;
        }

        private void MoveJump()
        {
            jumpProgress = Mathf.Clamp01((Time.time - jumpStartedAt) / Mathf.Max(0.01f, data.jumpDuration));
            float travelProgress = Mathf.Clamp01(jumpProgress / 0.75f);
            Vector2 destination = jumpStartPosition + jumpDirection * Mathf.Max(0f, data.jumpDistance) * travelProgress;
            Vector2 offset = destination - body.position;
            if (offset.sqrMagnitude > 0.000001f && TryExplodeAlongPath(offset.normalized, offset.magnitude)) return;
            body.velocity = offset / Time.fixedDeltaTime;
            if (jumpProgress < 1f) return;
            body.position = destination;
            body.velocity = Vector2.zero;
            ResetJumpVisual();
            tankRenderer.enabled = true;
            tankAnimator.enabled = true;
            tankAnimator.SetBool(IsMoving, false);
            tankAnimator.Play("Idle", 0, 0f);
        }

        private void SetFacing(float horizontalDirection)
        {
            if (Mathf.Abs(horizontalDirection) > 0.01f)
                tankRenderer.flipX = data.invertFacing ? horizontalDirection > 0f : horizontalDirection < 0f;
        }

        private void ResetJumpVisual()
        {
            isJumping = false;
            jumpDirection = Vector2.zero;
            jumpStartPosition = Vector2.zero;
            jumpStartedAt = 0f;
            jumpProgress = 0f;
            if (jumpVisual == null) return;
            jumpVisual.localPosition = Vector3.zero;
            jumpVisual.gameObject.SetActive(false);
        }

        private bool IsValidTarget()
        {
            return target != null && TryResolveEnemy(targetCollider, out _, out Transform currentTarget) &&
                   currentTarget == target &&
                   ((Vector2)target.position - body.position).sqrMagnitude <= data.range * data.range;
        }

        private void FindClosestEnemy()
        {
            target = null;
            targetCollider = null;
            overlaps.Clear();
            Physics2D.OverlapCircle(body.position, Mathf.Max(0f, data.range), enemyFilter, overlaps);
            float closestDistance = Mathf.Max(0f, data.range) * Mathf.Max(0f, data.range);
            foreach (Collider2D hit in overlaps)
            {
                if (!TryResolveEnemy(hit, out _, out Transform candidate)) continue;
                float distance = ((Vector2)candidate.position - body.position).sqrMagnitude;
                if (distance > closestDistance) continue;
                closestDistance = distance;
                target = candidate;
                targetCollider = hit;
            }
        }

        private bool TryResolveEnemy(Collider2D hit, out IDamageable damageable, out Transform enemyTransform)
        {
            damageable = hit != null ? hit.GetComponentInParent<IDamageable>() : null;
            enemyTransform = null;
            if (!(damageable is MonoBehaviour behaviour) || !behaviour.isActiveAndEnabled ||
                !hit.enabled || (hit.isTrigger && hit.transform != behaviour.transform) ||
                (behaviour is EnemyBase enemy && enemy.isDead) ||
                ((data.enemyLayers.value & (1 << hit.gameObject.layer)) == 0) ||
                (!hit.CompareTag("Enemy") && !behaviour.CompareTag("Enemy"))) return false;
            enemyTransform = behaviour.transform;
            return true;
        }

        private bool IsDetonationContact(Collider2D other)
        {
            if (other == null || !other.enabled || other.transform.IsChildOf(transform)) return false;
            if (TryResolveEnemy(other, out _, out _)) return true;
            // 감지용 트리거, 바닥, 플레이어는 벽으로 취급하지 않습니다.
            if (other.isTrigger) return false;
            if ((data.obstacleLayers.value & (1 << other.gameObject.layer)) != 0) return true;
            for (Transform parent = other.transform; parent != null; parent = parent.parent)
                if (parent.CompareTag("Wall")) return true;
            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (data != null && !hasExploded && IsDetonationContact(other)) Explode();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (data != null && !hasExploded && IsDetonationContact(other)) Explode();
        }

        private void Explode()
        {
            if (hasExploded || data == null) return;
            hasExploded = true;
            ResetJumpVisual();
            body.velocity = Vector2.zero;
            transform.position = new Vector3(body.position.x, body.position.y - 0.2f, transform.position.z);
            bodyCollider.enabled = false;
            body.simulated = false;
            tankAnimator.enabled = false;
            tankRenderer.enabled = false;
            target = null;
            targetCollider = null;
            explosionVisual.SetActive(true);
            explosionAnimator.Rebind();
            explosionAnimator.Play("Explosion", 0, 0f);

            overlaps.Clear();
            damagedTargets.Clear();
            damageTargets.Clear();
            Physics2D.OverlapCircle(body.position, Mathf.Max(0.01f, data.explosionRadius) * effectSizeMultiplier, enemyFilter, overlaps);
            foreach (Collider2D hit in overlaps)
                if (TryResolveEnemy(hit, out IDamageable damageable, out _) && damagedTargets.Add(damageable))
                    damageTargets.Add(damageable);
            foreach (IDamageable damageable in damageTargets)
                if (damageable is MonoBehaviour behaviour && behaviour.isActiveAndEnabled)
                    damageable.TakeDamage(Mathf.Max(0f, data.damage));
        }

        public void OnExplosionEnd()
        {
            explosionVisual.SetActive(false);
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.simulated = false;
            }
            if (explosionVisual != null) explosionVisual.SetActive(false);
            ResetJumpVisual();
            data = null;
            target = null;
            targetCollider = null;
            overlaps.Clear();
            castHits.Clear();
            damagedTargets.Clear();
            damageTargets.Clear();
        }
    }
}
