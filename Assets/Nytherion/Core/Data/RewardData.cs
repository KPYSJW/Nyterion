using System;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Relics;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class RewardData
    {
        public RewardType rewardType;
        public int amount = 1;
        public SkillData skillData;
        public ItemData itemData;
        public RelicData relicData;
    }
}
