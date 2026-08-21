using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Combat;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CollisionObject))]
    [RequireComponent(typeof(Collider2D))]
    public class LightningProj : MonoBehaviour, IProj, IProjModifier
    {
        [Header("Chain Lightning Config")]
        [Tooltip("최대 연쇄 타겟 수")]
        [SerializeField] private int maxChainCount = 4;

        [Tooltip("연쇄가 일어날 수 있는 적 간의 거리")]
        [SerializeField] private float chainRange = 4.5f;

        [Tooltip("번개 이펙트 프리팹")]
        [SerializeField] private ChainLightningVFX lightningEffectPrefab;

        [Tooltip("연쇄 적 타격 시 스폰할 스파크 이펙트 프리팹 (풀링 사용)")]
        [SerializeField] private GameObject sparkEffectPrefab;


        private Rigidbody2D rb;
        private CollisionObject collisionObj;
        private Collider2D myCollider;
        private float speed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            collisionObj = GetComponent<CollisionObject>();
            myCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            if (myCollider != null)
            {
                myCollider.enabled = true;
            }
        }

        // IProj 구현: RangedWeapon이 속도를 가할 때 호출됨
        public void SetSpeed(float speed)
        {
            this.speed = speed;
            if (rb != null)
            {
                rb.velocity = (Vector2)transform.right * speed;
            }
        }

        // IProjModifier 구현: CollisionObject가 충돌 시 OnHit를 호출함
        public bool OnHit(Collider2D target)
        {
            if (target.CompareTag("Enemy"))
            {
                // 충돌한 첫 번째 적을 기준으로 연쇄 번개 발동
                TriggerChainLightning(target.transform);
                
                // 첫 충돌 즉시 소멸(풀 반환)하도록 false 반환
                return false;
            }
            else if (target.CompareTag("Wall"))
            {
                // 벽 충돌 시 즉시 소멸하도록 false 반환
                return false;
            }

            return true;
        }

        // 맞은 적을 기점으로 연쇄 번개 연출 및 데미지 적용
        private void TriggerChainLightning(Transform firstTarget)
        {
            Transform startTransform = transform; // 최초 충돌 지점
            Vector3 startPos = startTransform.position;

            List<Transform> chainList = new List<Transform>();
            HashSet<Transform> visited = new HashSet<Transform>();

            chainList.Add(firstTarget);
            visited.Add(firstTarget);

            Transform currentSource = firstTarget;

            // 1. 연쇄 추적 로직 (가장 가까운 미방문 적 탐색)
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

            // 2. 실제 데미지 프로세싱
            float baseDamage = collisionObj != null ? collisionObj.damage : 10f;
            float currentDamage = baseDamage;

            foreach (Transform target in chainList)
            {
                if (target != null)
                {
                    IDamageable targetDamageable = target.GetComponent<IDamageable>();
                    if (targetDamageable != null)
                    {
                        targetDamageable.TakeDamage(currentDamage);
                    }

                    // 적 타격 위치에 스파크 이펙트 재생 (헬퍼 경유)
                    if (ObjectPoolManager.Instance != null)
                    {
                        WeaponVFXHelper.PlayHitEffect(sparkEffectPrefab, target.position);
                    }
                }
                currentDamage *= 0.8f; // 연쇄당 데미지 감쇄
            }

            // 3. 번개 연결 이펙트 연출
            SpawnLightningTrackingVFX(firstTarget, chainList);
        }

        private void SpawnLightningTrackingVFX(Transform startTransform, List<Transform> targets)
        {
            if (lightningEffectPrefab == null) return;

            // 충돌한 적의 부모나 월드에 생성
            ChainLightningVFX effect = Instantiate(lightningEffectPrefab, startTransform.position, Quaternion.identity);
            effect.Setup(startTransform, targets, null);
            Destroy(effect.gameObject, effect.duration + 0.1f);
        }
    }
}
