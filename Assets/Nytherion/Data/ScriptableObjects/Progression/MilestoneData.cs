using UnityEngine;
using Nytherion.Core.Enums;

namespace Nytherion.Data.ScriptableObjects.Progression
{
    [CreateAssetMenu(fileName = "NewMilestone", menuName = "Data/Progression/Milestone")]
    public class MilestoneData : ScriptableObject
    {
        [Header("Milestone Info")]
        public string milestoneID;
        public string title;
        [TextArea(3, 5)]
        public string description;
        public Sprite icon;

        [Header("Prerequisites")]
        public MilestoneData[] requiredMilestones;

        [Header("Progress Settings")]
        [Min(1)]
        public int targetValue = 1; 

        [Header("Rewards")]
        public SkillType rewardSkill = SkillType.None;
    }
}