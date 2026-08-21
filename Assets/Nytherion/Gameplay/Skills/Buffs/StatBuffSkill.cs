using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Core.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 일정 시간 동안 플레이어의 지정된 스탯들을 상승시키는 버프 스킬
    /// </summary>
    public class StatBuffSkill : SkillBase
    {
        private StatBuffSkillData statBuffData;
        private PlayerManager playerManager;
        private Coroutine activeBuffCoroutine;
        private bool isBuffApplied = false;

        // 적용된 버프 정보를 추적하여 나중에 제거할 때 사용
        private List<StatModifier> appliedModifiers = new List<StatModifier>();

        private void Start()
        {
            playerManager = FindObjectOfType<PlayerManager>();
        }

        protected override void Activate()
        {
            if (skillData != null)
            {
                statBuffData = skillData as StatBuffSkillData;
                if (statBuffData == null)
                {
                    Debug.LogError("[StatBuffSkill] SkillData가 StatBuffSkillData 타입이 아닙니다.");
                    return;
                }

                if (playerManager != null)
                {
                    // 버프 시간 갱신 처리를 위해 기존 코루틴 정지
                    if (activeBuffCoroutine != null)
                    {
                        StopCoroutine(activeBuffCoroutine);
                    }
                    activeBuffCoroutine = StartCoroutine(BuffRoutine());
                }
                else
                {
                    Debug.LogError("[StatBuffSkill] PlayerManager를 찾을 수 없습니다.");
                }
            }
        }

        /// <summary>
        /// 스탯 보넛스를 적용하고 지속 시간 후 원상복구하는 코루틴
        /// </summary>
        private IEnumerator BuffRoutine()
        {
            // 버프가 적용되어 있지 않은 상태라면 새로 적용
            if (!isBuffApplied)
            {
                appliedModifiers.Clear();

                // 데이터에 정의된 모든 스탯 버프를 순회하며 최종 버프 값을 계산하여 적용
                foreach (StatModifier modifier in statBuffData.statModifiers)
                {
                    // 스킬 레벨에 비례하여 증가치 계산
                    float finalValue = modifier.value + (modifier.valuePerLevel * Mathf.Max(0, statBuffData.skillLevel - 1));
                    
                    // 적용할 모디파이어 인스턴스 생성
                    StatModifier scaledModifier = new StatModifier {
                        stat = modifier.stat,
                        value = finalValue,
                        valuePerLevel = modifier.valuePerLevel,
                        isPercentage = modifier.isPercentage
                    };

                    // 해제용 리스트에 보관 및 플레이어에게 실제 스탯 적용
                    appliedModifiers.Add(scaledModifier);
                    playerManager.AddTemporaryStatModifier(scaledModifier);
                }
                isBuffApplied = true;
                Debug.Log($"[StatBuffSkill] 스탯 버프 활성화! (지속시간: {statBuffData.buffDuration}초, 스킬레벨: {statBuffData.skillLevel})");
            }
            else
            {
                // 이미 버프가 걸려있다면 새로 중첩시키지 않고 지속 시간만 갱신
                Debug.Log($"[StatBuffSkill] 스탯 버프 지속시간 갱신!");
            }

            // 지속 시간 대기
            yield return new WaitForSeconds(statBuffData.buffDuration);

            RemoveBuffs();
        }

        /// <summary>
        /// 적용되었던 모든 임시 스탯 버프를 제거하여 플레이어의 스탯을 원상복구
        /// </summary>
        private void RemoveBuffs()
        {
            if (isBuffApplied && playerManager != null)
            {
                foreach (StatModifier modifier in appliedModifiers)
                {
                    playerManager.RemoveTemporaryStatModifier(modifier);
                }
                appliedModifiers.Clear();
                isBuffApplied = false;
                Debug.Log("[StatBuffSkill] 스탯 버프 종료!");
            }
        }

        private void OnDestroy()
        {
            // 스킬 오브젝트 파괴 시 스탯이 영구적으로 남는것을 방지
            if (isBuffApplied)
            {
                RemoveBuffs();
            }
        }
    }
}
