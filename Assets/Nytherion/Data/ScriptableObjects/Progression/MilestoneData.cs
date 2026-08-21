using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Core.Data;
using System.Collections.Generic;
using Nytherion.Core.Utils;

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
        public string DisplayTitle => LocalizationText.Get(
            LocalizationTables.Progression,
            LocalizationKeys.MilestoneTitle(milestoneID),
            title,
            title);
        public string Description => LocalizationText.Get(
            LocalizationTables.Progression,
            LocalizationKeys.MilestoneDescription(milestoneID),
            description,
            description);
        public Sprite icon;
        public Sprite DisplayIcon
        {
            get
            {
                if (rewards != null)
                {
                    foreach (RewardData reward in rewards)
                    {
                        if (reward != null &&
                            reward.rewardType == RewardType.Relic &&
                            reward.relicData != null &&
                            reward.relicData.Image != null)
                        {
                            return reward.relicData.Image;
                        }
                    }
                }

                return icon;
            }
        }

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
