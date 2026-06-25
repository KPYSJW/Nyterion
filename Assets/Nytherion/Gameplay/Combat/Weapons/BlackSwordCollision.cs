using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class BlackSwordCollision : MonoBehaviour
    {
        [HideInInspector] public float damage;
        [HideInInspector] public List<EquipmentTrait> traits = new List<EquipmentTrait>();
        public GameObject hitEffectPrefab;
        
        [Header("Collision & Event Settings")]
        [SerializeField] private Collider2D col;
        [Tooltip("애니메이션 이벤트가 실패할 경우를 대비한 최대 생존 시간(안전 장치)")]
        [SerializeField] private float maxLifetime = 2.0f;
        [SerializeField] private string poolTag = "BlackSword_Slash_Effect";

        private Coroutine safetyReturnCoroutine;

        private void Awake()
        {
            poolTag = gameObject.name.Replace("(Clone)", "").Trim();
            if (col == null)
            {
                col = GetComponent<Collider2D>();
            }
        }

        private void OnEnable()
        {
            // 초기 상태는 콜라이더 비활성화
            DisableHitbox();

            // 애니메이션 이벤트 누락 대비 안전 장치 코루틴 구동
            if (safetyReturnCoroutine != null)
            {
                StopCoroutine(safetyReturnCoroutine);
            }
            safetyReturnCoroutine = StartCoroutine(SafetyReturnRoutine());
        }

        private IEnumerator SafetyReturnRoutine()
        {
            yield return new WaitForSeconds(maxLifetime);
            ReturnToPool();
        }

        // ==========================================
        // 애니메이션 이벤트(Animation Event) 수신용 메서드
        // ==========================================

        /// <summary>
        /// 애니메이션의 타격 시작 프레임에서 호출하여 콜라이더를 활성화합니다.
        /// </summary>
        public void EnableHitbox()
        {
            if (col != null)
            {
                col.enabled = true;
            }
        }

        /// <summary>
        /// 애니메이션의 타격 종료 프레임에서 호출하여 콜라이더를 비활성화합니다.
        /// </summary>
        public void DisableHitbox()
        {
            if (col != null)
            {
                col.enabled = false;
            }
        }

        /// <summary>
        /// 애니메이션이 끝나는 프레임에서 호출하여 이펙트를 풀로 반환합니다.
        /// </summary>
        public void OnAnimationEnd()
        {
            ReturnToPool();
        }

        // ==========================================

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Enemy"))
            {
                IDamageable target = collision.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(damage);
                    ApplyStatusEffects(collision.gameObject);
                    
                    Vector2 hitPoint = collision.ClosestPoint(transform.position);
                    WeaponEffectHelper.PlayHitEffect(hitEffectPrefab, hitPoint);
                }
            }
        }

        private void ApplyStatusEffects(GameObject targetObj)
        {
            StatusEffectManager effectManager = targetObj.GetComponent<StatusEffectManager>();
            if (effectManager == null)
            {
                effectManager = targetObj.AddComponent<StatusEffectManager>();
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
