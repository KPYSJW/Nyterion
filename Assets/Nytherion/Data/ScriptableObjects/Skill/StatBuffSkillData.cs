using UnityEngine;
using Nytherion.Core.Data;
using System.Collections.Generic;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "NewStatBuffData", menuName = "Data/Skill/StatBuff")]
    public class StatBuffSkillData : SkillData
    {
        [Header("Stat Buff Settings")]
        public float buffDuration = 5f;
        
        [Header("능력치 변경")]
        public List<StatModifier> statModifiers = new List<StatModifier>();
    }
}
