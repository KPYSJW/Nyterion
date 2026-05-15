using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Effects;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 필드에 생성된 터렛의 수명, 최대 생성 개수 관리 및 적군 탐색/공격 로직을 제어하는 클래스
    /// </summary>
    public class TurretController : MonoBehaviour
    {
        /// <summary> 현재 필드에 활성화된 모든 터렛을 관리하는 전역 리스트 </summary>
        private static List<TurretController> activeTurrets = new List<TurretController>();

        private float maxCount;
        private float duration;
        private float attackInterval;
        private float attackRange;
        private float damage;
        private string projectilePoolTag;
        private float projectileSpeed;

        private float lifetimeTimer;
        private float attackTimer;
        private static readonly Collider2D[] turretBuffer = new Collider2D[20];

        /// <summary>
        /// 터렛 생성 직후 호출되어 스킬 데이터를 기반으로 내부 스탯 초기화
        /// </summary>
        /// <param name="data">터렛 설정이 담긴 ScriptableObject 데이터</param>
        public void Initialize(TurretSkillData data)
        {
            this.maxCount = data.maxTurretCount;
            this.duration = data.duration;
            this.attackInterval = data.attackInterval;
            this.attackRange = data.range;
            this.damage = data.damage;
            this.projectilePoolTag = data.projectilePoolTag;
            this.projectileSpeed = data.projectileSpeed;

            // 타이머 초기화
            this.lifetimeTimer = duration;
            this.attackTimer = attackInterval;
        }

        private void Start()
        {
            // 생성된 터렛을 '활성화된 터렛 목록'의 마지막에 추가하여 추적 시작
            activeTurrets.Add(this);

            // 최대 소환 개수(maxCount) 초과 방지 로직
            if (activeTurrets.Count > maxCount)
            {
                // 목록의 첫 번째 요소(가장 오래된 터렛) 파괴
                TurretController oldestTurret = activeTurrets[0];
                if (oldestTurret != null)
                {
                    Destroy(oldestTurret.gameObject);
                }
                // 목록에서 파괴된 터렛 제거
                activeTurrets.RemoveAt(0);
            }
        }

        private void Update()
        {
            // 수명 타이머를 감소시키고, 0 이하가 되면 터렛 파괴
            lifetimeTimer -= Time.deltaTime;
            if (lifetimeTimer <= 0)
            {
                DestroyTurret();
                return;
            }

            // 공격 주기 타이머를 감소시키고, 0 이하가 되면 공격 수행
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                PerformAttack();
                // 공격 완료 후 타이머를 주기(attackInterval)로 재설정
                attackTimer = attackInterval;
            }
        }

        /// <summary>
        /// 탐색 반경 내의 적을 찾아 가장 가까운 적을 향해 투사체 발사
        /// </summary>
        private void PerformAttack()
        {
            // 공격 반경(attackRange) 내에 있는 모든 2D 콜라이더 탐색
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, attackRange, turretBuffer);
            
            IDamageable closestEnemy = null;
            float minDistanceSqr = float.MaxValue;
            Transform targetTransform = null;

            // 탐색된 콜라이더 중 가장 가까운 "Enemy" 태그를 가진 적 선별
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = turretBuffer[i];
                if (hit.CompareTag("Enemy"))
                {
                    IDamageable damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float distanceSqr = (transform.position - hit.transform.position).sqrMagnitude;
                        if (distanceSqr < minDistanceSqr)
                        {
                            minDistanceSqr = distanceSqr;
                            closestEnemy = damageable;
                            targetTransform = hit.transform;
                        }
                    }
                }
            }

            // 유효한 타겟이 존재한다면 투사체 발사
            if (closestEnemy != null && targetTransform != null)
            {
                // 적을 향하는 방향 벡터 계산
                Vector3 direction = (targetTransform.position - transform.position).normalized;
                
                // 방향 벡터를 기반으로 Z축 회전값(Quaternion) 계산
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0, 0, angle);

                if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(projectilePoolTag))
                {
                    // 오브젝트 풀에서 투사체 호출
                    GameObject projObj = ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, transform.position, rotation);
                    if (projObj != null)
                    {
                        // 투사체 속도 설정 (Rigidbody2D 활용)
                        if (projObj.TryGetComponent<Rigidbody2D>(out var rb))
                        {
                            rb.velocity = direction * projectileSpeed;
                        }

                        // 투사체 데미지 및 풀 태그 초기화
                        if (projObj.TryGetComponent<CollisionObject>(out var collisionObj))
                        {
                            collisionObj.damage = this.damage;
                            
                            if (string.IsNullOrEmpty(collisionObj.poolTag))
                            {
                                collisionObj.poolTag = this.projectilePoolTag;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[Turret] 투사체 프리팹에 CollisionObject 컴포넌트가 누락되었습니다! Tag: {projectilePoolTag}");
                        }

                        // 투사체의 최대 사거리 제한 컴포넌트 설정 및 추가
                        if (!projObj.TryGetComponent<ProjectileDistanceLimit>(out var distanceLimit))
                        {
                            distanceLimit = projObj.AddComponent<ProjectileDistanceLimit>();
                        }
                        distanceLimit.Initialize(attackRange);
                    }
                }
                else
                {
                    Debug.LogWarning("[Turret] ObjectPoolManager가 초기화되지 않았거나 투사체 태그가 설정되지 않았습니다.");
                }
            }
        }

        /// <summary>
        /// 터렛을 전역 리스트에서 제거하고 오브젝트 파괴
        /// </summary>
        private void DestroyTurret()
        {
            if (activeTurrets.Contains(this))
            {
                activeTurrets.Remove(this);
            }
            Destroy(gameObject);
        }

        /// <summary>
        /// 외부 요인으로 파괴될 경우를 대비한 안전 장치
        /// </summary>
        private void OnDestroy()
        {
            if (activeTurrets.Contains(this))
            {
                activeTurrets.Remove(this);
            }
        }
    }
}