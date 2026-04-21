using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "NewLifestealBuffData", menuName = "Data/Skill/LifestealBuff")]
    public class LifestealBuffSkillData : SkillData
    {
        [Header("Lifesteal Buff Settings")]
        public float buffDuration = 5f;
        [Tooltip("적에게 입힌 데미지 중 체력으로 회복되는 비율 (예: 0.5 = 50%)")]
        public float healRatio = 0.5f;
        [Tooltip("스킬 레벨당 추가 회복 비율")]
        public float healRatioPerLevel = 0.1f;
    }
}
