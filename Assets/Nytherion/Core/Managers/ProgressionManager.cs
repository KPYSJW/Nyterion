using System;
using UnityEngine;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Progression;

namespace Nytherion.Core.Managers
{
    public interface IProgressionManager
    {
        bool IsSkillUnlocked(SkillType skillType);
        void UnlockSkill(SkillType skillType);
        bool IsMilestoneCompleted(string milestoneId);
        void CompleteMilestone(string milestoneId);
        void CompleteMilestone(MilestoneData milestone);
        bool IsMilestoneAvailable(MilestoneData milestone);

        int GetCurrentProgress(string milestoneId);
        void AddProgress(MilestoneData milestone, int amount = 1);
        event Action<string, int, int> OnMilestoneProgressUpdated;

        event Action<SkillType> OnSkillUnlocked;
        event Action<string> OnMilestoneCompleted;
    }

    public class ProgressionManager : BaseManager, IProgressionManager, ISaveable 
    {
        private ProgressionState state = new ProgressionState();

        public event Action<SkillType> OnSkillUnlocked;
        public event Action<string> OnMilestoneCompleted;
        public event Action<string, int, int> OnMilestoneProgressUpdated;
        public event Action OnProgressionDataLoaded;
        public bool IsSkillUnlocked(SkillType skillType)
        {
            return state.unlockedSkills.Contains(skillType);
        }

        public void UnlockSkill(SkillType skillType)
        {
            if (!state.unlockedSkills.Contains(skillType))
            {
                state.unlockedSkills.Add(skillType);

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

                OnMilestoneCompleted?.Invoke(milestoneId);
            }
        }

        public void CompleteMilestone(MilestoneData milestone)
        {
            if (milestone == null) return;

            if (!state.completedMilestones.Contains(milestone.milestoneID))
            {
                state.completedMilestones.Add(milestone.milestoneID);

                if (milestone.rewardSkill != SkillType.None)
                {
                    UnlockSkill(milestone.rewardSkill);
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
                OnProgressionDataLoaded?.Invoke();
            }
        }
    }
}