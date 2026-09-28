using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Combat;
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
        protected float attackRange;
        protected float damage;
        private string projectilePoolTag;
        private float projectileSpeed;
        private GameObject projectilePrefab;
        private Vector3 projectileSpawnOffset;
        private bool isInitialized;

        private float lifetimeTimer;
        private float attackTimer;
        private static readonly Collider2D[] turretBuffer = new Collider2D[20];

        /// <summary>
        /// 터렛 생성 직후 호출되어 스킬 데이터를 기반으로 내부 스탯 초기화
        /// </summary>
        /// <param name="data">터렛 설정이 담긴 ScriptableObject 데이터</param>
        public virtual void Initialize(TurretSkillData data)
        {
            this.maxCount = data.maxTurretCount;
            this.duration = data.duration;
            this.attackInterval = data.attackInterval;
            this.attackRange = data.range;
            this.damage = data.damage;
            this.projectilePoolTag = data.projectilePoolTag;
            this.projectileSpeed = data.projectileSpeed;
            this.projectilePrefab = data.projectilePrefab;
            this.projectileSpawnOffset = data.projectileSpawnOffset;

            // 타이머 초기화
            this.lifetimeTimer = duration;
            this.attackTimer = attackInterval;
            isInitialized = true;
        }

        /// <summary>
        /// 기본 포탑은 착지 지점에 즉시 배치하고, 연출이 있는 포탑은 이 메서드를 확장합니다.
        /// </summary>
        public virtual void Deploy(Vector3 launchPosition, Vector3 landingPosition)
        {
            transform.position = landingPosition;
        }

        private void Start()
        {
            if (!isInitialized)
            {
                return;
            }
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

        protected virtual void Update()
        {
            if (!isInitialized)
            {
                return;
            }
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
        protected virtual void PerformAttack()
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
                LaunchProjectileAtTarget(targetTransform);
            }
        }

        /// <summary>
        /// 애니메이션 이벤트에서도 호출할 수 있도록 발사 동작을 분리합니다.
        /// </summary>
        protected void LaunchProjectileAtTarget(Transform target)
        {
            if (target == null || ObjectPoolManager.Instance == null ||
                (projectilePrefab == null && string.IsNullOrEmpty(projectilePoolTag)))
            {
                return;
            }

            Vector3 spawnPosition = transform.TransformPoint(projectileSpawnOffset);
            Vector2 direction = (target.position - spawnPosition).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            GameObject projectile = projectilePrefab != null
                ? ObjectPoolManager.Instance.SpawnFromPool(projectilePrefab, spawnPosition, rotation)
                : ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, spawnPosition, rotation);
            if (projectile == null)
            {
                return;
            }

            if (projectile.TryGetComponent(out Rigidbody2D rigidbody))
            {
                rigidbody.velocity = direction * projectileSpeed;
            }
            if (projectile.TryGetComponent(out IProj projectileController))
            {
                projectileController.SetSpeed(projectileSpeed);
            }
            if (projectile.TryGetComponent(out CollisionObject collisionObject))
            {
                collisionObject.damage = damage;
                if (string.IsNullOrEmpty(collisionObject.poolTag))
                {
                    collisionObject.poolTag = projectilePrefab != null ? projectilePrefab.name : projectilePoolTag;
                }
            }
            if (!projectile.TryGetComponent(out ProjDistanceLimit distanceLimit))
            {
                distanceLimit = projectile.AddComponent<ProjDistanceLimit>();
            }
            distanceLimit.Initialize(attackRange);

            SpriteRenderer turretRenderer = GetComponentInChildren<SpriteRenderer>();
            if (turretRenderer != null && projectile.TryGetComponent(out SpriteRenderer projectileRenderer))
            {
                projectileRenderer.sortingLayerID = turretRenderer.sortingLayerID;
                projectileRenderer.sortingOrder = turretRenderer.sortingOrder + 1;
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
