using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class IceShotProjectile : MonoBehaviour
    {
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        private IceShotSkillData data;
        private SpriteRenderer spriteRenderer;
        private Vector2 direction;
        private float damage, size, flightTime, hitTime;
        private bool impacting;
        private Collider2D homingTarget;
        private EnemyBase homingEnemy;
        private float straightTimeRemaining, homingTurnSpeed;
        private bool targetWasAhead;

        public void Begin(IceShotSkillData skill, Vector3 origin, Vector2 aim, float castDamage, float castSize,
            bool testHoming = false, float turnSpeed = 180f, float straightTime = 0f)
        {
            data = skill;
            direction = aim.normalized;
            damage = castDamage;
            size = Mathf.Max(0.01f, castSize);
            flightTime = 0f;
            hitTime = 0f;
            impacting = false;
            homingTarget = testHoming ? HomingProj.FindClosestEnemy(origin, 10f) : null;
            homingEnemy = homingTarget != null ? homingTarget.GetComponentInParent<EnemyBase>() : null;
            targetWasAhead = homingTarget != null && Vector2.Dot(direction,
                (Vector2)homingTarget.bounds.center - (Vector2)origin) >= 0f;
            homingTurnSpeed = Mathf.Max(0f, turnSpeed);
            straightTimeRemaining = Mathf.Max(0f, straightTime);
            transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            transform.localScale = Vector3.one * (size * Mathf.Max(0.01f, data.visualScale));
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = data.projectileFrames[0];
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (impacting)
            {
                hitTime += Time.deltaTime;
                int frame = Mathf.FloorToInt(hitTime * data.hitFps);
                if (frame >= data.hitFrames.Length) { gameObject.SetActive(false); return; }
                spriteRenderer.sprite = data.hitFrames[frame];
                return;
            }
            float step = Mathf.Min(Time.deltaTime, Mathf.Max(0f, data.projectileLifetime - flightTime));
            UpdateHoming(step);
            spriteRenderer.sprite = data.projectileFrames[Mathf.FloorToInt(flightTime * data.projectileFps) % data.projectileFrames.Length];
            float distance = Mathf.Max(0f, data.speed) * step;
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
                        status.ApplyEffect(new IceEffect(5f));
                    StartImpact(hit.point);
                    return;
                }
                int layer = hit.collider.gameObject.layer;
                if (layer == LayerMask.NameToLayer("Wall") || layer == LayerMask.NameToLayer("Obstacle"))
                { StartImpact(hit.point); return; }
            }
            transform.position += (Vector3)(direction * distance);
            flightTime += step;
            if (flightTime >= data.projectileLifetime) StartImpact(transform.position);
        }

        private void StartImpact(Vector2 position)
        {
            // 스프라이트의 위쪽을 진행 방향에 맞추고 아래쪽 피벗을 타격 위치에 둡니다.
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.SetPositionAndRotation(new Vector3(position.x, position.y, transform.position.z), Quaternion.Euler(0f, 0f, angle));
            transform.localScale = Vector3.one * (size * Mathf.Max(0.01f, data.hitVisualScale));
            impacting = true;
            hitTime = 0f;
            spriteRenderer.sprite = data.hitFrames[0];
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
    }
}
