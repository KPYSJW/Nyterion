using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CollisionObject))]
    [RequireComponent(typeof(Collider2D))]
    public class IceShotProjectile : MonoBehaviour, IProjectile, IProjectileEffect
    {
        [Header("Animation Trigger Names")]
        [SerializeField] private string launchTrigger = "Launch";
        [SerializeField] private string hitTrigger = "Hit";

        [Header("Relic Split Settings")]
        [SerializeField] private string requiredRelicName = "Glacial Prism";
        [SerializeField] private string requiredRelicKoreanName = "빙결 프리즘";
        public bool canSplit = true;

        [Header("Split Physics Settings")]
        public int splitCount = 3;
        public float splitAngle = 60f;
        public float splitSpeedMultiplier = 1.0f;
        public float splitDamageMultiplier = 0.5f;

        private Rigidbody2D rb;
        private CollisionObject collisionObj;
        private Collider2D myCollider;
        private Animator animator;

        private float speed;
        private bool isMoving;
        private bool isHit;
        private Collider2D ignoredCollider;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            collisionObj = GetComponent<CollisionObject>();
            myCollider = GetComponent<Collider2D>();
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void OnEnable()
        {
            isMoving = false;
            isHit = false;
            if (myCollider != null)
            {
                myCollider.enabled = true;
            }
            // 발사 시점에 강제 고정을 보장하기 위해 velocity 초기화
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            
            // 유물 장착 상태 검사 (분열 여부 실시간 갱신)
            CheckRelicSplit();
            
            Debug.Log("[IceShotProjectile] OnEnable - Ready to launch. canSplit: " + canSplit);
        }

        private void OnDisable()
        {
            // 풀로 반환되어 비활성화될 때 물리적 충돌 무시 설정을 복구
            ResetIgnoredCollider();
        }

        // IProjectile 구현: RangedWeapon 등에서 호출됨
        public void SetSpeed(float speed)
        {
            this.speed = speed;
            // RangedWeapon이 rb.velocity를 강제로 주입하므로, 
            // Start 상태에 머물러야 하기 때문에 강제로 속도를 0으로 덮어씀
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            Debug.Log($"[IceShotProjectile] SetSpeed called. Speed: {speed}");
        }

        // 유물 장착 상태를 검사하여 분열 기능 활성화 여부를 세팅
        private void CheckRelicSplit()
        {
            // 이미 2차 분열이 방지된 자식 투사체(canSplit = false)는 검사에서 제외
            if (!canSplit) return;

            // 기본적으로는 유물이 없으므로 false로 초기화
            canSplit = false;

            PlayerManager player = FindObjectOfType<PlayerManager>();
            if (player != null)
            {
                PlayerRelicManager relicManager = player.GetComponent<PlayerRelicManager>();
                if (relicManager != null)
                {
                    List<RelicData> relics = relicManager.GetCurrentRelics();
                    foreach (RelicData relic in relics)
                    {
                        if (relic != null)
                        {
                            bool matchesEnglish = !string.IsNullOrEmpty(requiredRelicName) && 
                                                 string.Equals(relic.relicName, requiredRelicName, System.StringComparison.OrdinalIgnoreCase);
                            
                            bool matchesKorean = !string.IsNullOrEmpty(requiredRelicKoreanName) && 
                                                 string.Equals(relic.koreanName, requiredRelicKoreanName, System.StringComparison.OrdinalIgnoreCase);

                            if (matchesEnglish || matchesKorean)
                            {
                                canSplit = true;
                                Debug.Log($"[IceShotProjectile] Split feature ENABLED by Relic: {relic.koreanName} ({relic.relicName})");
                                break;
                            }
                        }
                    }
                }
            }
        }

        // 특정 콜라이더와의 물리적 충돌을 무시하도록 설정
        public void SetIgnoredCollider(Collider2D targetCollider)
        {
            ignoredCollider = targetCollider;
            if (myCollider != null && ignoredCollider != null)
            {
                Physics2D.IgnoreCollision(myCollider, ignoredCollider, true);
                Debug.Log($"[IceShotProjectile] Collision ignored with {ignoredCollider.name}");
            }
        }

        // 특정 콜라이더와의 물리적 충돌 무시를 해제하고 초기화
        private void ResetIgnoredCollider()
        {
            if (myCollider != null && ignoredCollider != null)
            {
                Physics2D.IgnoreCollision(myCollider, ignoredCollider, false);
                Debug.Log($"[IceShotProjectile] Collision restored with {ignoredCollider.name}");
            }
            ignoredCollider = null;
        }

        // 애니메이션 이벤트: Start 애니메이션 종료 시점에 호출
        public void LaunchProjectile()
        {
            if (isHit) return;

            isMoving = true;
            if (rb != null)
            {
                rb.velocity = (Vector2)transform.right * speed;
            }
            Debug.Log("[IceShotProjectile] LaunchProjectile! Velocity: " + (rb != null ? rb.velocity : Vector2.zero));

            if (animator != null && !string.IsNullOrEmpty(launchTrigger))
            {
                animator.SetTrigger(launchTrigger);
            }
        }

        // IProjectileEffect 구현: CollisionObject가 충돌 시 OnHit를 호출함
        public bool OnHit(Collider2D target)
        {
            Debug.Log($"[IceShotProjectile] OnHit called with target: {target.name}, Tag: {target.tag}");
            
            // 이미 충돌이 완료된 상태면 true 리턴해서 소멸 방지
            if (isHit) return true;

            isHit = true;
            isMoving = false;
            
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            if (myCollider != null)
            {
                myCollider.enabled = false;
            }

            // 분열 처리 (적 충돌 시에만 분열 및 canSplit이 켜져 있을 때만 분열)
            if (target.CompareTag("Enemy") && canSplit)
            {
                TriggerSplit(target);
            }

            if (animator != null && !string.IsNullOrEmpty(hitTrigger))
            {
                Debug.Log($"[IceShotProjectile] Triggering animator hit. TriggerName: {hitTrigger}");
                animator.SetTrigger(hitTrigger);
            }
            else
            {
                Debug.LogWarning("[IceShotProjectile] Animator is null or hitTrigger is empty. Destroying instantly.");
                FinishHit();
            }

            // true를 반환하면 CollisionObject가 ReturnToPool()을 즉시 호출하지 않음
            return true;
        }

        // 적과 충돌 시 부채꼴 분열 처리
        private void TriggerSplit(Collider2D target)
        {
            if (collisionObj == null) return;

            string targetPoolTag = collisionObj.poolTag;
            if (string.IsNullOrEmpty(targetPoolTag)) return;

            // 현재 날아가는 진행 방향 계산
            Vector2 currentDir = transform.right;
            if (rb != null && rb.velocity.sqrMagnitude > 0.1f)
            {
                currentDir = rb.velocity.normalized;
            }

            float baseAngle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
            float startAngle = baseAngle - (splitAngle / 2f);
            float angleStep = splitCount > 1 ? splitAngle / (splitCount - 1) : 0f;

            Debug.Log($"[IceShotProjectile] Splitting into {splitCount} projectiles (ignoring {target.name}).");

            for (int i = 0; i < splitCount; i++)
            {
                float currentA = startAngle + (angleStep * i);
                Vector2 spreadDirection = new Vector2(
                    Mathf.Cos(currentA * Mathf.Deg2Rad),
                    Mathf.Sin(currentA * Mathf.Deg2Rad)
                );

                GameObject fragment = ObjectPoolManager.Instance.SpawnFromPool(targetPoolTag, transform.position, Quaternion.identity);

                if (fragment != null)
                {
                    fragment.transform.rotation = Quaternion.AngleAxis(currentA, Vector3.forward);

                    // 자식 IceShotProjectile 컴포넌트 처리
                    if (fragment.TryGetComponent<IceShotProjectile>(out IceShotProjectile childProj))
                    {
                        childProj.canSplit = false; // 2차 분열 방지
                        childProj.SetSpeed(speed * splitSpeedMultiplier); // 속도 설정
                        
                        // 방금 맞춘 적과의 충돌 무시 처리
                        childProj.SetIgnoredCollider(target);
                        
                        childProj.LaunchProjectile(); // 즉시 발사 및 Repeat 애니메이션 재생
                    }

                    // 데미지 배율 적용
                    if (fragment.TryGetComponent<CollisionObject>(out CollisionObject fragCol))
                    {
                        fragCol.damage = collisionObj.damage * splitDamageMultiplier;
                    }
                }
            }
        }

        // 애니메이션 이벤트: Hit 애니메이션 종료 시점에 호출
        public void FinishHit()
        {
            if (collisionObj != null)
            {
                collisionObj.ReturnToPool();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
