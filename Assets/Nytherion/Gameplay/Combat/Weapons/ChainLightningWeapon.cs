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

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;
            lastAttackTime = Time.time;
            PlayFireAnimation();

            Transform startTransform = firePoint != null ? firePoint : transform;
            Vector3 startPos = startTransform.position;

            // 1. 사거리 안의 가장 가까운 첫 번째 적색 타겟 탐색
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

            // 첫 적이 없는 경우 허공에 빔 발사 연출 (시작 앵커 연동)
            if (firstTarget == null)
            {
                Vector3 airEnd = startPos + (Vector3)direction.normalized * weaponData.range;
                SpawnLightningStaticVFX(startTransform, airEnd);
                return;
            }

            // 2. 연쇄 추적 로직 (가장 가까운 미방문 적 탐색)
            List<Transform> chainList = new List<Transform>();
            HashSet<Transform> visited = new HashSet<Transform>();

            chainList.Add(firstTarget);
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
                    chainList.Add(nextTarget);
                    visited.Add(nextTarget);
                    currentSource = nextTarget;
                }
                else
                {
                    break;
                }
            }

            // 3. 실제 데미지 프로세싱
            float currentDamage = weaponData.damage * damageMultiplier;

            foreach (Transform target in chainList)
            {
                IDamageable targetDamageable = target.GetComponent<IDamageable>();
                if (targetDamageable != null)
                {
                    targetDamageable.TakeDamage(currentDamage);
                }

                currentDamage *= 0.8f; 
            }

            // 4. 실시간 트래킹 이펙트 호출
            SpawnLightningTrackingVFX(startTransform, chainList);
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

            // 허공 샷도 시작 앵커를 부모로 삼아 생성하고 월드 앵커 정보를 갱신하도록 처리
            ChainLightningEffect effect = Instantiate(lightningEffectPrefab, startTransform.position, Quaternion.identity, startTransform);
            
            List<Vector3> staticPoints = new List<Vector3>();
            staticPoints.Add(endPoint);
            
            effect.Setup(startTransform, null, staticPoints);
            Destroy(effect.gameObject, effect.duration + 0.1f);
        }

        public override void AttackEnd()
        {
        }
    }
}
