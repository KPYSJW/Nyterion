using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Skill;
using System;
using VContainer;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 유물 장착 시 스킬 UI 보관함에 해당 스킬이 즉시 추가되고 유물 레벨과 스킬 레벨이 동기화되며,
    /// 유물 장착 해제 시 스킬 UI 및 장착 슬롯에서 해당 스킬이 제거되는 효과
    /// </summary>
    [Serializable, RelicDisplayName("스킬 부여 효과")]
    public class GrantSkillEffect : RelicEffectBase
    {
        [Tooltip("부여할 스킬 데이터")]
        public SkillData skillData;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            if (skillData == null) return;

            SkillDataManager skillDataManager = GetSkillDataManager();
            if (skillDataManager != null)
            {
                skillDataManager.GrantSkillFromRelic(skillData, level);
                Debug.Log($"[GrantSkillEffect] 스킬 부여 완료: {skillData.skillName} (스킬 레벨: {level})");
            }
            else
            {
                Debug.LogWarning("[GrantSkillEffect] SkillDataManager를 찾을 수 없어 스킬 부여에 실패했습니다.");
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (skillData == null) return;

            SkillDataManager skillDataManager = GetSkillDataManager();
            if (skillDataManager != null)
            {
                skillDataManager.RevokeSkillFromRelic(skillData);
                Debug.Log($"[GrantSkillEffect] 스킬 회수 완료: {skillData.skillName}");
            }
        }

        private SkillDataManager GetSkillDataManager()
        {
            if (RootLifetimeScope.Instance != null && RootLifetimeScope.Instance.Container != null)
            {
                SkillDataManager manager = null;
                if (RootLifetimeScope.Instance.Container.TryResolve<SkillDataManager>(out manager))
                {
                    return manager;
                }
            }

            return UnityEngine.Object.FindObjectOfType<SkillDataManager>();
        }
    }
}
