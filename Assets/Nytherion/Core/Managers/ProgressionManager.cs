using System;
using UnityEngine;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.Data.ScriptableObjects.Skill;

namespace Nytherion.Core.Managers
{
    public interface IProgressionManager
    {
        bool IsSkillUnlocked(SkillData skillData);
        bool IsSkillUnlocked(string skillId);
        void UnlockSkill(SkillData skillData);
        bool IsMilestoneCompleted(string milestoneId);
        void CompleteMilestone(string milestoneId);
        void CompleteMilestone(MilestoneData milestone);
        bool IsMilestoneAvailable(MilestoneData milestone);

        int GetCurrentProgress(string milestoneId);
        void AddProgress(MilestoneData milestone, int amount = 1);
        event Action<string, int, int> OnMilestoneProgressUpdated;

        event Action<SkillData> OnSkillUnlocked;
        event Action<string> OnMilestoneCompleted;
    }

    public class ProgressionManager : BaseManager, IProgressionManager, ISaveable 
    {
        private ProgressionState state = new ProgressionState();

        public event Action<SkillData> OnSkillUnlocked;
        public event Action<string> OnMilestoneCompleted;
        public event Action<string, int, int> OnMilestoneProgressUpdated;
        public event Action OnProgressionDataLoaded;
        
        public bool IsSkillUnlocked(SkillData skillData)
        {
            return skillData != null && state.unlockedSkills.Contains(skillData.skillID);
        }

        public bool IsSkillUnlocked(string skillId)
        {
            return !string.IsNullOrEmpty(skillId) && state.unlockedSkills.Contains(skillId);
        }

        public void UnlockSkill(SkillData skillData)
        {
            if (skillData != null && !state.unlockedSkills.Contains(skillData.skillID))
            {
                state.unlockedSkills.Add(skillData.skillID);

                OnSkillUnlocked?.Invoke(skillData);
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
                Debug.Log($"[Progression] 마일스톤 완료: {milestoneId}");

                OnMilestoneCompleted?.Invoke(milestoneId);
            }
        }

        public void CompleteMilestone(MilestoneData milestone)
        {
            if (milestone == null) return;

            if (!state.completedMilestones.Contains(milestone.milestoneID))
            {
                state.completedMilestones.Add(milestone.milestoneID);

                if (milestone.rewards != null)
                {
                    foreach (var reward in milestone.rewards)
                    {
                        if (reward.rewardType == RewardType.Skill && reward.skillData != null)
                        {
                            UnlockSkill(reward.skillData);
                        }
                    }
                }

                OnMilestoneCompleted?.Invoke(milestone.milestoneID);
            }
        }
        
        public bool IsMilestoneAvailable(MilestoneData milestone)
        {
            if (milestone == null) return false;

            if (IsMilestoneCompleted(milestone.milestoneID)) return false;

            if (milestone.requiredMilestones != null)
            {
                foreach (var req in milestone.requiredMilestones)
                {
                    if (req != null && !IsMilestoneCompleted(req.milestoneID))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public int GetCurrentProgress(string milestoneId)
        {
            if (IsMilestoneCompleted(milestoneId)) return -1; 

            var progress = state.activeProgresses.Find(p => p.milestoneID == milestoneId);
            return progress != null ? progress.currentValue : 0;
        }

        public void AddProgress(MilestoneData milestone, int amount = 1)
        {
            if (milestone == null || IsMilestoneCompleted(milestone.milestoneID)) return;

            var progress = state.activeProgresses.Find(p => p.milestoneID == milestone.milestoneID);
            if (progress == null)
            {
                progress = new MilestoneProgressEntry { milestoneID = milestone.milestoneID, currentValue = 0 };
                state.activeProgresses.Add(progress);
            }

            progress.currentValue += amount;

            if (progress.currentValue > milestone.targetValue)
                progress.currentValue = milestone.targetValue;


            OnMilestoneProgressUpdated?.Invoke(milestone.milestoneID, progress.currentValue, milestone.targetValue);

            if (progress.currentValue >= milestone.targetValue)
            {
                CompleteMilestone(milestone); 

                state.activeProgresses.Remove(progress);
            }
        }
        // --- ISaveable ---
        public override void PopulateSaveData(SaveData saveData)
        {
            saveData.progressionState = this.state;
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            if (saveData.progressionState != null)
            {
                this.state = saveData.progressionState;
                OnProgressionDataLoaded?.Invoke();
            }
        }
    }
}
