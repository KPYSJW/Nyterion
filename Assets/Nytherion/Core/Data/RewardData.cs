using System;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Skill;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class RewardData
    {
        public RewardType rewardType;
        public int amount = 1;
        public SkillData skillData;
    }
}
