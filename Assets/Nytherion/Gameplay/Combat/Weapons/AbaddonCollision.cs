using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Combat.Weapon
{
    /// <summary>
    /// Abaddon 슬래시 이펙트 프리팹의 충돌 판정 및 수명 주기를 제어합니다.
    /// 애니메이션 이벤트를 통해 충돌 활성화/비활성화 시점을 제어하며, 
    /// 자체 콜라이더 또는 자식 객체의 콜라이더 충돌을 모아서 단일 공격 범위 내 중복 타격을 방지합니다.
    /// </summary>
    public class AbaddonCollision : MonoBehaviour
    {
        [HideInInspector] public float damage;
        [HideInInspector] public List<EquipmentTrait> traits = new List<EquipmentTrait>();
        public GameObject hitEffectPrefab;

        [Header("Collision & Event Settings")]
        [Tooltip("타격 판정을 수행할 콜라이더 목록. 비어있을 경우 Awake 시 자동 탐색합니다.")]
        [SerializeField] private List<Collider2D> targetColliders = new List<Collider2D>();
        [Tooltip("애니메이션 이벤트 누락 등을 대비한 안전 반환 시간(초)")]
        [SerializeField] private float maxLifetime = 2.0f;
        
        private string poolTag;
        private Coroutine safetyReturnCoroutine;
        private HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

        private void Awake()
        {
            poolTag = gameObject.name.Replace("(Clone)", "").Trim();
            
            // 등록된 콜라이더가 없을 경우 자기 자신 및 자식의 모든 Collider2D를 수집합니다.
            if (targetColliders.Count == 0)
            {
                Collider2D[] foundColliders = GetComponentsInChildren<Collider2D>(true);
                foreach (Collider2D found in foundColliders)
                {
                    targetColliders.Add(found);
                }
            }
        }

        private void OnEnable()
        {
            // 초기 상태는 콜라이더 비활성화 및 타격 대상 클리어
            DisableHitbox();

            // 애니메이션 이벤트 누락을 대비해 실제 재생 시간 계산 후 자동 풀 반환 구동
            if (safetyReturnCoroutine != null)
            {
                StopCoroutine(safetyReturnCoroutine);
            }
            safetyReturnCoroutine = StartCoroutine(AutoReturnRoutine());
        }

        private void OnDisable()
        {
            if (safetyReturnCoroutine != null)
            {
                StopCoroutine(safetyReturnCoroutine);
                safetyReturnCoroutine = null;
            }
        }

        private IEnumerator AutoReturnRoutine()
        {
            // 애니메이터 상태가 첫 프레임에 갱신되지 않았을 수 있으므로 1프레임 대기
            yield return null;

            float duration = maxLifetime;
            Animator anim = GetComponent<Animator>();
            if (anim == null)
            {
                anim = GetComponentInChildren<Animator>();
            }

            if (anim != null)
            {
                AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
                duration = state.length;
            }

            yield return new WaitForSeconds(duration);
            ReturnToPool();
        }

        // ==========================================
        // 애니메이션 이벤트(Animation Event) 연동 메서드
        // ==========================================

        /// <summary>
        /// 타격 시작 프레임에서 애니메이션 이벤트를 통해 호출하여 콜라이더를 활성화합니다.
        /// </summary>
        public void EnableHitbox()
        {
            hitTargets.Clear();
            foreach (Collider2D col in targetColliders)
            {
                if (col != null)
                {
                    col.enabled = true;
                }
            }
        }

        /// <summary>
        /// 타격 종료 프레임에서 애니메이션 이벤트를 통해 호출하여 콜라이더를 비활성화합니다.
        /// </summary>
        public void DisableHitbox()
        {
            foreach (Collider2D col in targetColliders)
            {
                if (col != null)
                {
                    col.enabled = false;
                }
            }
            hitTargets.Clear();
        }

        /// <summary>
        /// 애니메이션 완료 프레임에서 애니메이션 이벤트를 통해 호출하여 오브젝트를 풀로 반환합니다.
        /// </summary>
        public void OnAnimationEnd()
        {
            ReturnToPool();
        }

        // ==========================================
        // 충돌 및 데미지 전달 처리
        // ==========================================

        /// <summary>
        /// 자체 OnTriggerEnter2D 또는 자식의 AbaddonChildCollision을 통해 호출되어 적에게 데미지를 적용합니다.
        /// </summary>
        public void ProcessTriggerEnter(Collider2D collision)
        {
            if (collision.CompareTag("Enemy"))
            {
                IDamageable target = collision.GetComponent<IDamageable>();
                if (target != null)
                {
                    // 중복 타격 방지
                    if (!hitTargets.Contains(target))
                    {
                        hitTargets.Add(target);
                        target.TakeDamage(damage);
                        ApplyStatusEffects(collision.gameObject);

                        Vector2 hitPoint = collision.ClosestPoint(transform.position);
                        WeaponEffectHelper.PlayHitEffect(hitEffectPrefab, hitPoint);
                    }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            ProcessTriggerEnter(collision);
        }

        private void ApplyStatusEffects(GameObject targetObj)
        {
            StatusEffectManager effectManager = targetObj.GetComponent<StatusEffectManager>();
            if (effectManager == null)
            {
                return;
            }

            if (traits.Contains(EquipmentTrait.Fire))
            {
                float burnDamage = Mathf.Max(1f, damage * 0.2f);
                effectManager.ApplyEffect(new FireEffect(burnDamage, 5f));
            }
            if (traits.Contains(EquipmentTrait.Curse))
            {
                effectManager.ApplyEffect(new CurseEffect(1.1f, 5f));
            }
            if (traits.Contains(EquipmentTrait.Ice))
            {
                effectManager.ApplyEffect(new IceEffect(5f));
            }
            if (traits.Contains(EquipmentTrait.Lightning))
            {
                effectManager.ApplyEffect(new LightningEffect(5f));
            }
            if (traits.Contains(EquipmentTrait.Holy))
            {
                effectManager.ApplyEffect(new HolyEffect(5f));
            }
            if (traits.Contains(EquipmentTrait.Demonic))
            {
                effectManager.ApplyEffect(new DemonicEffect(5f));
            }
            if (traits.Contains(EquipmentTrait.Poison))
            {
                effectManager.ApplyEffect(new PoisonEffect(3f, 5f));
            }
        }

        public void ReturnToPool()
        {
            if (safetyReturnCoroutine != null)
            {
                StopCoroutine(safetyReturnCoroutine);
                safetyReturnCoroutine = null;
            }

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
