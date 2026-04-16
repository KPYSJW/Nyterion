using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;

namespace Nytherion.Core.Managers
{
    public interface IProgressionManager
    {
        bool IsSkillUnlocked(SkillType skillType);
        void UnlockSkill(SkillType skillType);
        bool IsMilestoneCompleted(string milestoneId);
        void CompleteMilestone(string milestoneId);

        event Action<SkillType> OnSkillUnlocked;
    }

    public class ProgressionManager : BaseManager, IProgressionManager, ISaveable 
    {
        private ProgressionState state = new ProgressionState();

        public event Action<SkillType> OnSkillUnlocked;

        public bool IsSkillUnlocked(SkillType skillType)
        {
            return state.unlockedSkills.Contains(skillType);
        }

        public void UnlockSkill(SkillType skillType)
        {
            if (!state.unlockedSkills.Contains(skillType))
            {
                state.unlockedSkills.Add(skillType);
                Debug.Log($"[Progression] 새로운 스킬 해금됨: {skillType}");

                OnSkillUnlocked?.Invoke(skillType);
            }
        }

        public bool IsMilestoneCompleted(string milestoneId)
        {
            return state.completedMilestones.Contains(milestoneId);
        }

        public void CompleteMilestone(string milestoneId)
        {
            if (!state.completedMilestones.Contains(milestoneId))
            {
                state.completedMilestones.Add(milestoneId);
                Debug.Log($"[Progression] 마일스톤 달성: {milestoneId}");

                
            }
        }

        // --- ISaveable 구현부 ---
        public override void PopulateSaveData(SaveData saveData)
        {
            saveData.progressionState = this.state;
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            if (saveData.progressionState != null)
            {
                this.state = saveData.progressionState;
            }
        }
    }
}