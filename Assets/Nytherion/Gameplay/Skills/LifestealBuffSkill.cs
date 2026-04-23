using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using System.Collections;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 일정 시간 동안 플레이어가 적에게 입힌 피해의 일정 비율만큼 체력을 회복하는 흡혈 버프 스킬
    /// 이벤트 매니저를 통해 적 타격 이벤트를 구독하여 처리
    /// </summary>
    public class LifestealBuffSkill : SkillBase
    {
        private LifestealBuffSkillData lifestealData;
        private EventManager eventManager;
        private PlayerHealth playerHealth;
        private bool isBuffActive = false;

        private void Start()
        {
            eventManager = FindObjectOfType<EventManager>();
        }

        protected override void Activate()
        {
            if (skillData != null)
            {
                lifestealData = skillData as LifestealBuffSkillData;
                if (lifestealData == null)
                {
                    Debug.LogError("[LifestealBuffSkill] SkillData가 LifestealBuffSkillData 타입이 아닙니다.");
                    return;
                }

                // 시전자의 체력 컴포넘트 획득
                if (caster != null)
                {
                    playerHealth = caster.GetComponent<PlayerHealth>();
                }

                if (playerHealth != null && eventManager != null)
                {
                    StartCoroutine(BuffRoutine());
                }
                else
                {
                    Debug.LogError("[LifestealBuffSkill] PlayerHealth 또는 EventManager를 찾을 수 없습니다.");
                }
            }
        }

        /// <summary>
        /// 버프의 지속 시간을 제어하고 타격 이벤트를 구독/해제하는 코루틴
        /// </summary>
        /// <returns></returns>
        private IEnumerator BuffRoutine()
        {
            // 이미 버프가 동작 중이라면 중복 적용 방지
            if (isBuffActive)
            {
                yield break;
            }

            isBuffActive = true;
            // 적 타격 이벤트 구독 (플레이어가 적에게 피해를 입힐 때마다 HandleEnemyDamaged 메서드 호출)
            eventManager.OnEnemyDamagedByPlayer += HandleEnemyDamaged;

            Debug.Log($"[LifestealBuffSkill] 흡혈 버프 활성화! (지속시간: {lifestealData.buffDuration}초)");

            // 지속 시간 대기
            yield return new WaitForSeconds(lifestealData.buffDuration);

            isBuffActive = false;
            // 버프 종료 시 이벤트 구독 해제
            eventManager.OnEnemyDamagedByPlayer -= HandleEnemyDamaged;

            Debug.Log("[LifestealBuffSkill] 흡혈 버프 종료!");
        }

        /// <summary>
        /// 적이 플레이어에게 피해를 입었을 때 호출되는 콜백 메서드
        /// </summary>
        /// <param name="damageAmount">적에게 가한 데미지</param>
        private void HandleEnemyDamaged(float damageAmount)
        {
            if (isBuffActive && playerHealth != null && lifestealData != null)
            {
                // 스킬 레벨에 따른 최종 흡혈 비율 계산
                float finalHealRatio = lifestealData.healRatio + (lifestealData.healRatioPerLevel * Mathf.Max(0, lifestealData.skillLevel - 1));
                float healAmount = damageAmount * finalHealRatio;

                // 플레이어 체력 회복
                playerHealth.Heal(healAmount);
                Debug.Log($"[LifestealBuffSkill] 흡혈 효과 발생: {healAmount} 회복 (비율: {finalHealRatio * 100}%, 스킬레벨: {lifestealData.skillLevel})");
            }
        }

        private void OnDestroy()
        {
            // 오브젝트 파괴 시 메모리 누수 방지를 위해 이벤트 구독 해제
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayer -= HandleEnemyDamaged;
            }
        }
    }
}
