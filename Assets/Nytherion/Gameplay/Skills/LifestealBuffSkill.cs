using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using System.Collections;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
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

        private IEnumerator BuffRoutine()
        {
            if (isBuffActive)
            {
                yield break;
            }

            isBuffActive = true;
            eventManager.OnEnemyDamagedByPlayer += HandleEnemyDamaged;

            Debug.Log($"[LifestealBuffSkill] 흡혈 버프 활성화! (지속시간: {lifestealData.buffDuration}초)");

            yield return new WaitForSeconds(lifestealData.buffDuration);

            isBuffActive = false;
            eventManager.OnEnemyDamagedByPlayer -= HandleEnemyDamaged;

            Debug.Log("[LifestealBuffSkill] 흡혈 버프 종료!");
        }

        private void HandleEnemyDamaged(float damageAmount)
        {
            if (isBuffActive && playerHealth != null && lifestealData != null)
            {
                float finalHealRatio = lifestealData.healRatio + (lifestealData.healRatioPerLevel * Mathf.Max(0, lifestealData.skillLevel - 1));
                float healAmount = damageAmount * finalHealRatio;
                playerHealth.Heal(healAmount);
                Debug.Log($"[LifestealBuffSkill] 흡혈 효과 발생: {healAmount} 회복 (비율: {finalHealRatio * 100}%, 스킬레벨: {lifestealData.skillLevel})");
            }
        }

        private void OnDestroy()
        {
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayer -= HandleEnemyDamaged;
            }
        }
    }
}
