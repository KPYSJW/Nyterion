using UnityEngine;
using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat
{
    public class ChainLightningWeapon : RangedWeapon
    {
        [Header("Chain Lightning Config")]
        [Tooltip("최대 연쇄 타겟 수")]
        public int maxChainCount = 4;

        [Tooltip("연쇄가 일어날 수 있는 적 간의 거리")]
        public float chainRange = 4.5f;

        [Tooltip("번개 이펙트 프리팹")]
        public ChainLightningEffect lightningEffectPrefab;

        [Header("Continuous Stream Config")]
        [Tooltip("마우스를 누르는 동안 지속적으로 번개 빔이 나가는 모드를 활성화할지 여부")]
        public bool isContinuousStream = true;

        [Tooltip("지속 공격 시 데미지를 가할 주기 (초)")]
        public float tickInterval = 0.2f;

        private ChainLightningEffect activeEffect;
        private bool isAttacking = false;
        private float tickTimer = 0f;
        private List<Transform> activeTargets = new List<Transform>();
        private List<Vector3> activeStaticPoints = new List<Vector3>();

        public override bool CanAttack()
        {
            // 지속 스트림 모드이면서 현재 공격(누르고 있는) 중이면
            // 매 프레임 마우스 및 조준 방향을 갱신받아야 하므로 쿨다운을 우회하여 true 반환
            if (isContinuousStream && isAttacking)
            {
                return true;
            }
            return base.CanAttack();
        }

        private void Update()
        {
            if (!isContinuousStream || !isAttacking) return;

            // 틱 타이머 체크
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0)
            {
                ApplyTickDamage();
                tickTimer = tickInterval;
            }
        }

        private void OnDisable()
        {
            // 무기가 해제되거나 비활성화될 때 이펙트 정리
            AttackEnd();
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (isContinuousStream)
            {
                // 지속형 번개 스트림 모드 작동
                if (!isAttacking)
                {
                    isAttacking = true;
                    tickTimer = 0f; // 즉시 첫 피해 적용
                    
                    Transform startTransform = firePoint != null ? firePoint : transform;
                    if (lightningEffectPrefab != null)
                    {
                        activeEffect = Instantiate(lightningEffectPrefab, startTransform.position, Quaternion.identity, startTransform);
                        activeEffect.duration = 99999f; // 강제로 수명 시간을 아주 늘려서 자동 파괴 방지
                    }
                }

                // 매 프레임 실시간 번개 연결 상태 갱신
                UpdateTargetsAndVisuals(direction);
            }
            else
            {
                // 기존 단발성 체인 라이트닝 모드 작동 -> 이제는 투사체 발사 방식으로 동작
                if (!CanAttack()) return;
                lastAttackTime = Time.time;
                FireProjectiles(direction, 1);
            }
        }

        // 매 프레임 타겟 추적 상태와 이펙트 라인 연결을 갱신
        private void UpdateTargetsAndVisuals(Vector2 direction)
        {
            Transform startTransform = firePoint != null ? firePoint : transform;
            Vector3 startPos = startTransform.position;

            Collider2D[] initialHits = Physics2D.OverlapCircleAll(startPos, weaponData.range);
            Transform firstTarget = null;
            float closestDistance = Mathf.Infinity;

            foreach (Collider2D hit in initialHits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    float dist = Vector3.Distance(startPos, hit.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        firstTarget = hit.transform;
                    }
                }
            }

            // 첫 타겟이 없으면 마우스 방향으로 번개 궤적 유지
            if (firstTarget == null)
            {
                activeTargets.Clear();
                activeStaticPoints.Clear();
                Vector3 airEnd = startPos + (Vector3)direction.normalized * weaponData.range;
                activeStaticPoints.Add(airEnd);

                if (activeEffect != null)
                {
                    activeEffect.Setup(startTransform, null, activeStaticPoints);
                }
                return;
            }

            // 연쇄 적들 추적
            activeTargets.Clear();
            activeStaticPoints.Clear();
            HashSet<Transform> visited = new HashSet<Transform>();

            activeTargets.Add(firstTarget);
            visited.Add(firstTarget);

            Transform currentSource = firstTarget;

            for (int i = 1; i < maxChainCount; i++)
            {
                Collider2D[] chainHits = Physics2D.OverlapCircleAll(currentSource.position, chainRange);
                Transform nextTarget = null;
                float closestChainDist = Mathf.Infinity;

                foreach (Collider2D hit in chainHits)
                {
                    if (hit.CompareTag("Enemy") && !visited.Contains(hit.transform))
                    {
                        float dist = Vector3.Distance(currentSource.position, hit.transform.position);
                        if (dist < closestChainDist)
                        {
                            closestChainDist = dist;
                            nextTarget = hit.transform;
                        }
                    }
                }

                if (nextTarget != null)
                {
                    activeTargets.Add(nextTarget);
                    visited.Add(nextTarget);
                    currentSource = nextTarget;
                }
                else
                {
                    break;
                }
            }

            // 번개 이펙트 실시간 연결 갱신
            if (activeEffect != null)
            {
                activeEffect.Setup(startTransform, activeTargets, null);
            }
        }

        // 일정 주기 틱마다 번개에 닿아있는 타겟들에게 일제히 데미지 부여
        private void ApplyTickDamage()
        {
            float currentDamage = weaponData.damage * damageMultiplier;

            foreach (Transform target in activeTargets)
            {
                if (target != null)
                {
                    IDamageable targetDamageable = target.GetComponent<IDamageable>();
                    if (targetDamageable != null)
                    {
                        targetDamageable.TakeDamage(currentDamage);
                        ApplyStatusEffects(targetDamageable);
                    }
                }
                currentDamage *= 0.8f;
            }
        }

        private void SpawnLightningTrackingVFX(Transform startTransform, List<Transform> targets)
        {
            if (lightningEffectPrefab == null) return;

            ChainLightningEffect effect = Instantiate(lightningEffectPrefab, startTransform.position, Quaternion.identity, startTransform);
            effect.Setup(startTransform, targets, null);
            Destroy(effect.gameObject, effect.duration + 0.1f);
        }

        private void SpawnLightningStaticVFX(Transform startTransform, Vector3 endPoint)
        {
            if (lightningEffectPrefab == null) return;

            ChainLightningEffect effect = Instantiate(lightningEffectPrefab, startTransform.position, Quaternion.identity, startTransform);
            
            List<Vector3> staticPoints = new List<Vector3>();
            staticPoints.Add(endPoint);
            
            effect.Setup(startTransform, null, staticPoints);
            Destroy(effect.gameObject, effect.duration + 0.1f);
        }

        public override void AttackEnd()
        {
            if (isContinuousStream)
            {
                if (isAttacking)
                {
                    isAttacking = false;
                    if (activeEffect != null)
                    {
                        Destroy(activeEffect.gameObject);
                        activeEffect = null;
                    }
                    activeTargets.Clear();
                    activeStaticPoints.Clear();
                }
            }
            else
            {
                // 단발형은 별도 정리 없음
            }
        }
    }
}
