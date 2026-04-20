using System.Collections.Generic;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Data
{
    [System.Serializable]
    public class MilestoneProgressEntry
    {
        public string milestoneID;
        public int currentValue;
    }

    [System.Serializable]
    public class ProgressionState
    {
        public List<SkillType> unlockedSkills = new List<SkillType>();

        public List<string> completedMilestones = new List<string>();

        public List<MilestoneProgressEntry> activeProgresses = new List<MilestoneProgressEntry>();
    }
}