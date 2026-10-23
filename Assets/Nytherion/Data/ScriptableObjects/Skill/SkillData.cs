using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Core.Utils;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "Data/Skill")]
    public class SkillData : ScriptableObject
    {
        [Header("Skill ID")]
        public string skillID;

        [Tooltip("폐기된 스킬은 획득, 장착 및 저장 데이터 복원에서 제외합니다.")]
        public bool isDisabled;
        public bool IsRuntimeAvailable => !isDisabled;
        
        [Header("Skill Type")]
        public SkillType skillType;

        [Header("Skill Info")]
        public string skillName;
        [TextArea(3, 5)] 
        public string description;
        public string DisplayName => LocalizationText.Get(
            LocalizationTables.Skills,
            LocalizationKeys.SkillName(skillID),
            skillName,
            skillName);
        public string Description => LocalizationText.Get(
            LocalizationTables.Skills,
            LocalizationKeys.SkillDescription(skillID),
            description,
            description);
        public int skillLevel;
        public int exp;
        public float coolDown;
        public int manaCost;
        public float damage;
        public float range;

        [Header("Skill Icon")]
        public Sprite icon;

        [Header("Skill Prefab")]
        public GameObject skillPrefab;

        [Header("Unlock Settings")]
        [Tooltip("이 스킬을 해금하기 위해 필요한 마일스톤 ID (비어있으면 기본 해금)")]
        public string unlockMilestoneID;

    }
}
