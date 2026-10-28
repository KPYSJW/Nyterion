using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.Gameplay.Relics.Modules;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class WindBlastProjectile : MonoBehaviour, IProj
    {
        [SerializeField, Min(0.1f)] private float speed = 10f;
        [SerializeField, Min(0.1f)] private float lifetime = 2f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.22f;
        [SerializeField] private LayerMask obstacleLayers;
        private ObjectPoolManager pool;
        private Vector2 direction;
        private float damage;
        private float chainRatio;
        private float chainRadius;
        private int chainTargets;
        private ChainLightningVFX lightningPrefab;
        private float chainChance;
        private DashStormRuntime stormRuntime;
        private bool usesStormRuntime;
        private EnemyBase sourceEnemy;
        private float remaining;
        private bool flying;

        public void SetSpeed(float value) => speed = Mathf.Max(0.1f, value);

        public void Launch(ObjectPoolManager ownerPool, Vector2 travelDirection, float hitDamage,
            float size, float lightningRatio, float lightningRadius, int lightningTargets,
            ChainLightningVFX lightningEffectPrefab = null, float lightningChance = 1f,
            DashStormRuntime runtime = null, EnemyBase source = null)
        {
            pool = ownerPool;
            direction = travelDirection.normalized;
            damage = hitDamage;
            chainRatio = lightningRatio;
            chainRadius = lightningRadius;
            chainTargets = lightningTargets;
            lightningPrefab = lightningEffectPrefab;
            chainChance = Mathf.Clamp01(lightningChance);
            stormRuntime = runtime;
            usesStormRuntime = runtime != null;
            sourceEnemy = source;
            remaining = lifetime;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, size);
            // 시트의 회오리는 세로로 서 있는 모습을 유지합니다.
            Animator animator = GetComponent<Animator>();
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
            flying = true;
        }

        private void Update()
        {
            if (!flying) return;
            float distance = speed * Time.deltaTime;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position,
                hitRadius * transform.localScale.x, direction, distance);
            foreach (RaycastHit2D hit in hits)
            {
                EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
                if (enemy != null && enemy == sourceEnemy) continue;
                if (enemy != null && !enemy.isDead)
                {
                    // 보조 피해 이벤트의 재귀를 막고 연쇄 폭풍의 확률 판정만 직접 실행합니다.
                    flying = false;
                    enemy.TakeDamage(damage, true);
                    if (usesStormRuntime)
                    {
                        if (stormRuntime != null) stormRuntime.TryProjectileLightning(enemy, damage);
                    }
                    else if (DashStormRuntime.RollLightning(chainChance))
                        DashStormRuntime.SpreadLightning(enemy, damage * Mathf.Max(0f, chainRatio), chainRadius, chainTargets, lightningPrefab);
                    ReturnToPool();
                    return;
                }
                if (!hit.collider.isTrigger && (obstacleLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    ReturnToPool();
                    return;
                }
            }
            transform.position += (Vector3)direction * distance;
            remaining -= Time.deltaTime;
            if (remaining <= 0f) ReturnToPool();
        }

        private void ReturnToPool()
        {
            flying = false;
            if (pool != null && pool.TryGetPoolTag(gameObject, out string tag)) pool.ReturnToPool(tag, gameObject);
            else Destroy(gameObject);
        }

        private void OnDisable()
        {
            flying = false;
            pool = null;
            damage = chainRatio = chainRadius = 0f;
            chainTargets = 0;
            lightningPrefab = null;
            chainChance = 0f;
            stormRuntime = null;
            usesStormRuntime = false;
            sourceEnemy = null;
        }
    }
}
