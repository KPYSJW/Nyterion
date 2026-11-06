using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class LightboltProjectile : MonoBehaviour
    {
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        private LightboltSkillData data;
        private SpriteRenderer spriteRenderer;
        private SpiritsGuidanceTrail trail;
        private Vector2 direction;
        private float damage, size, flightTime, animationTime;
        private bool impacting;

        public void Begin(LightboltSkillData skill, Vector3 origin, Vector2 aim, float castDamage, float castSize)
        {
            data = skill;
            direction = aim.normalized;
            damage = castDamage;
            size = Mathf.Max(0.01f, castSize);
            flightTime = 0f;
            animationTime = 0f;
            impacting = false;
            transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            transform.localScale = Vector3.one * (size * Mathf.Max(0.01f, data.projectileVisualScale));
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = data.projectileFrames[0];
            gameObject.SetActive(true);
            if (trail == null) trail = GetComponent<SpiritsGuidanceTrail>();
            if (trail != null)
            {
                trail.enabled = true;
                trail.SetVariant(0, leaveWorldTrail: true);
            }
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

        private void StartImpact(Vector2 contactPoint)
        {
            if (trail != null) trail.enabled = false;
            // 타격 이펙트는 충돌 지점에서 투사체가 날아온 방향을 따릅니다.
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
