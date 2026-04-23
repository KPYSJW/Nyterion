using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 시전자를 따라다니는 영역을 생성하여 일정 주기마다 적에게 데미지를 주거나 적의 투사체를 파괴
    /// </summary>
    public class AuraSkill : SkillBase
    {
        private AuraSkillData auraData;
        private GameObject auraObject;
        private CircleCollider2D auraCollider;
        private Coroutine activeAuraCoroutine;

        // 오라 범위 내에 들어온 적들을 추적하기 위한 컬렉션 
        private HashSet<IDamageable> enemiesInRange = new HashSet<IDamageable>();
        private float nextTickTime;
        Rigidbody2D rb;
        private void Awake()
        {
            auraCollider = GetComponent<CircleCollider2D>();
            rb = GetComponent<Rigidbody2D>();
        }

        protected override void Activate()
        {
            if (skillData != null)
            {
                auraData = skillData as AuraSkillData;
                if (auraData == null)
                {
                    Debug.LogError("[AuraSkill] SkillData가 AuraSkillData 타입이 아닙니다.");
                    return;
                }

                // 이미 활성화된 오라가 있다면 기존 코루틴을 중지하고 새로 시작(지속시간 갱신)
                if (activeAuraCoroutine != null)
                {
                    StopCoroutine(activeAuraCoroutine);
                }
                activeAuraCoroutine = StartCoroutine(AuraRoutine());
            }
        }

        /// <summary>
        /// 오라의 지속 시간을 관리하고 주기적으로 데미지를 가하는 코루틴
        /// </summary>
        private IEnumerator AuraRoutine()
        {
            // 오라 오브젝트가 아직 생성되지 않았다면 생성
            if (auraObject == null)
            {
                CreateAuraObject();
            }

            // 스킬 레벨에 따른 최종 데미지 계산 (레벨은 1부터 시작하므로 -1 처리)
            float finalDamage = auraData.damagePerTick + (auraData.damagePerLevel * Mathf.Max(0, auraData.skillLevel - 1));

            auraCollider.radius = auraData.auraRadius;
            auraObject.SetActive(true);
            enemiesInRange.Clear(); // 범위 초기화
            nextTickTime = Time.time + auraData.tickRate;

            Debug.Log($"[AuraSkill] 오라 스킬 활성화! (지속시간: {auraData.auraDuration}초, 반경: {auraData.auraRadius}, 데미지: {finalDamage}, 투사체파괴: {auraData.destroyEnemyProjectiles})");

            float timer = auraData.auraDuration;
            while (timer > 0)
            {
                // 데미지 틱 타이머 체크 및 공격 수행
                if (Time.time >= nextTickTime && auraData.dealDamage)
                {
                    DealTickDamage(finalDamage);
                    nextTickTime = Time.time + auraData.tickRate;
                }

                timer -= Time.deltaTime;
                yield return null;
            }

            // 지속 시간이 끝나면 오라 비활성화
            auraObject.SetActive(false);
            Debug.Log("[AuraSkill] 오라 스킬 종료!");
        }


        /// <summary>
        /// 현재 범위 내에 있는 모든 적에게 일괄적으로 데미지를 준다
        /// </summary>
        private void DealTickDamage(float damage)
        {
            List<IDamageable> toRemove = new List<IDamageable>();

            foreach (var enemy in enemiesInRange)
            {
                // 적 오브젝트가 아직 유효하고 활성화 상태인지 확인
                if (enemy != null && enemy is MonoBehaviour mb && mb.gameObject.activeInHierarchy)
                {
                    enemy.TakeDamage(damage);
                }
                else
                {
                    toRemove.Add(enemy); // 유효하지 않은 적은 제거 목록에 추가
                }
            }

            // 파괴되었거나 비활성화된 적들을 추적 목록에서 일괄 제거 
            foreach (var r in toRemove)
            {
                enemiesInRange.Remove(r);
            }
        }

        /// <summary>
        /// 오라 영역의 물리적 충돌 판정을 처리할 자식 오브젝트와 컴포넌트를 동적으로 생성
        /// </summary>
        private void CreateAuraObject()
        {
            auraObject = new GameObject("AuraArea");
            auraObject.transform.SetParent(caster != null ? caster : transform);
            auraObject.transform.localPosition = Vector3.zero; // 시전자의 중심 위치에 고정

            // 실제 충돌 이벤트를 처리할 핸들러 부착
            AuraTriggerHandler triggerHandler = auraObject.AddComponent<AuraTriggerHandler>();
            triggerHandler.Initialize(this, auraData.destroyEnemyProjectiles);

            auraObject.SetActive(false);
        }

        public void AddEnemy(IDamageable enemy) => enemiesInRange.Add(enemy);
        public void RemoveEnemy(IDamageable enemy) => enemiesInRange.Remove(enemy);
    }

    /// <summary>
    ///  AuraSkill 영역 내의 Trigger 충돌을 독립적으로 감지하고 처리하는 핼퍼 클래스
    /// </summary>
    public class AuraTriggerHandler : MonoBehaviour
    {
        private AuraSkill parentSkill;
        private bool destroyProjectiles;

        public void Initialize(AuraSkill skill, bool destroy)
        {
            parentSkill = skill;
            destroyProjectiles = destroy;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // 적 감지 시 오라 범위 내 적 목록에 추가 
            if (collision.CompareTag("Enemy"))
            {
                IDamageable damageable = collision.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    parentSkill.AddEnemy(damageable);
                }
            }

            // 적 투사체 파괴 감지 설정이 켜져 있는 경우 투사체 즉시 파괴
            if (destroyProjectiles)
            {
                if (collision.GetComponent<EnemyProjectiles>() != null)
                {
                    Destroy(collision.gameObject);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            // 적이 오라 범위를 벗어나면 목록에서 제거 
            if (collision.CompareTag("Enemy"))
            {
                IDamageable damageable = collision.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    parentSkill.RemoveEnemy(damageable);
                }
            }
        }
    }
}
