using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Core.Data;
using System.Collections.Generic;

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

        [Header("Progress Settings")]
        public ProgressionType progressionType;
        [Min(1)]
        public int targetValue = 1;

        [Header("Prerequisites")]
        public MilestoneData[] requiredMilestones;

        [Header("Rewards")]
        public List<RewardData> rewards = new List<RewardData>();
    }
}