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

        [Header("사용 시 발밑 이펙트")]
        public Sprite[] activationFrames;
        [Min(1f)] public float activationFramesPerSecond = 16f;
        [Min(0.01f)] public float activationScale = 0.75f;
        public Vector2 activationFeetOffset;
    }
}
