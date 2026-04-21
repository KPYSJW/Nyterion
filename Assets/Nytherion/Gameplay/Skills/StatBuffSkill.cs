using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Core.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    public class StatBuffSkill : SkillBase
    {
        private StatBuffSkillData statBuffData;
        private PlayerManager playerManager;
        private Coroutine activeBuffCoroutine;
        private bool isBuffApplied = false;
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

        private IEnumerator BuffRoutine()
        {
            if (!isBuffApplied)
            {
                appliedModifiers.Clear();
                foreach (StatModifier modifier in statBuffData.statModifiers)
                {
                    float finalValue = modifier.value + (modifier.valuePerLevel * Mathf.Max(0, statBuffData.skillLevel - 1));
                    StatModifier scaledModifier = new StatModifier {
                        stat = modifier.stat,
                        value = finalValue,
                        valuePerLevel = modifier.valuePerLevel,
                        isPercentage = modifier.isPercentage
                    };
                    appliedModifiers.Add(scaledModifier);
                    playerManager.AddTemporaryStatModifier(scaledModifier);
                }
                isBuffApplied = true;
                Debug.Log($"[StatBuffSkill] 스탯 버프 활성화! (지속시간: {statBuffData.buffDuration}초, 스킬레벨: {statBuffData.skillLevel})");
            }
            else
            {
                Debug.Log($"[StatBuffSkill] 스탯 버프 지속시간 갱신!");
            }

            yield return new WaitForSeconds(statBuffData.buffDuration);

            RemoveBuffs();
        }

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
            if (isBuffApplied)
            {
                RemoveBuffs();
            }
        }
    }
}
