using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class FireballProjectile : MonoBehaviour
    {
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        private FireballSkillData data;
        private SpriteRenderer spriteRenderer;
        private Vector2 direction;
        private float damage, size, flightTime, animationTime;
        private bool impacting;
        private Collider2D homingTarget;
        private EnemyBase homingEnemy;
        private float straightTimeRemaining;
        private float homingTurnSpeed;
        private bool targetWasAhead;
        private const float HomingSearchRadius = 10f;

        public void Begin(FireballSkillData skill, Vector3 origin, Vector2 aim, float castDamage, float castSize,
            bool testHoming = false, float turnSpeed = 180f, float straightTime = 0f)
        {
            data = skill;
            direction = aim.normalized;
            damage = castDamage;
            size = Mathf.Max(0.01f, castSize);
            flightTime = 0f;
            animationTime = 0f;
            impacting = false;
            homingTarget = testHoming ? HomingProj.FindClosestEnemy(origin, HomingSearchRadius) : null;
            homingEnemy = homingTarget != null ? homingTarget.GetComponentInParent<EnemyBase>() : null;
            targetWasAhead = homingTarget != null && Vector2.Dot(direction,
                (Vector2)homingTarget.bounds.center - (Vector2)origin) >= 0f;
            homingTurnSpeed = Mathf.Max(0f, turnSpeed);
            straightTimeRemaining = Mathf.Max(0f, straightTime);
            transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            transform.localScale = Vector3.one * (size * Mathf.Max(0.01f, data.projectileVisualScale));
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = data.projectileFrames[0];
            gameObject.SetActive(true);
        }

        private void Update()
        {
            animationTime += Time.deltaTime;
            if (impacting)
            {
                int frame = Mathf.FloorToInt(animationTime * data.hitFps);
                if (frame >= data.hitFrames.Length) { gameObject.SetActive(false); return; }
                spriteRenderer.sprite = data.hitFrames[frame];
                return;
            }

            spriteRenderer.sprite = data.projectileFrames[Mathf.FloorToInt(animationTime * data.projectileFps) % data.projectileFrames.Length];
            float remaining = Mathf.Max(0f, data.projectileLifetime - flightTime);
            float stepTime = Mathf.Min(Time.deltaTime, remaining);
            UpdateHoming(stepTime);
            float distance = Mathf.Max(0f, data.speed) * stepTime;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(data.collisionLayers);
            hits.Clear();
            Physics2D.CircleCast(transform.position, data.collisionRadius * size, direction, filter, hits, distance);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null) continue;
                EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
                if (enemy != null)
                {
                    if (!enemy.isActiveAndEnabled || enemy.isDead) continue;
                    ((IDamageable)enemy).TakeDamage(damage);
                    if (!enemy.isDead && enemy.isActiveAndEnabled && enemy.TryGetComponent(out StatusEffectManager status))
                        status.ApplyEffect(new FireEffect(Mathf.Max(1f, damage * 0.2f), 5f));
                    StartImpact(hit.point);
                    return;
                }
                int layer = hit.collider.gameObject.layer;
                if (layer == LayerMask.NameToLayer("Wall") || layer == LayerMask.NameToLayer("Obstacle"))
                {
                    StartImpact(hit.point);
                    return;
                }
            }
            transform.position += (Vector3)(direction * distance);
            flightTime += stepTime;
            if (flightTime >= data.projectileLifetime) StartImpact(transform.position);
        }

        private void UpdateHoming(float deltaTime)
        {
            if (homingTarget == null || deltaTime <= 0f) return;
            if (!homingTarget.enabled || !homingTarget.gameObject.activeInHierarchy || homingEnemy == null ||
                !homingEnemy.isActiveAndEnabled || homingEnemy.isDead)
            {
                homingTarget = null;
                homingEnemy = null;
                return;
            }
            if (straightTimeRemaining > 0f)
            {
                straightTimeRemaining -= deltaTime;
                return;
            }
            Vector2 desired = (Vector2)homingTarget.bounds.center - (Vector2)transform.position;
            if (desired.sqrMagnitude < 0.0001f) return;
            bool targetIsAhead = Vector2.Dot(direction, desired) >= 0f;
            // 앞에 있던 적을 지나치면 되돌아가지 않고 진행 방향을 유지합니다.
            if (targetWasAhead && !targetIsAhead)
            {
                homingTarget = null;
                homingEnemy = null;
                return;
            }
            targetWasAhead |= targetIsAhead;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
            angle = Mathf.MoveTowardsAngle(angle, targetAngle, homingTurnSpeed * deltaTime);
            float radians = angle * Mathf.Deg2Rad;
            direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void StartImpact(Vector2 contactPoint)
        {
            // 불꽃은 충돌 지점에서 투사체가 날아온 방향을 따릅니다.
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.SetPositionAndRotation(new Vector3(contactPoint.x, contactPoint.y, transform.position.z), Quaternion.Euler(0f, 0f, angle));
            float hitScale = size * Mathf.Max(0.01f, data.hitVisualScale);
            transform.localScale = new Vector3(hitScale, hitScale * Mathf.Max(0.01f, data.hitVerticalScale), 1f);
            impacting = true;
            animationTime = 0f;
            spriteRenderer.sprite = data.hitFrames[0];
        }
    }
}
