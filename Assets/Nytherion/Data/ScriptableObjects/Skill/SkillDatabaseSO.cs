using UnityEngine;
using System.Collections.Generic;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "Data/Skill Database")]
    public class SkillDatabaseSO : ScriptableObject
    {
        public List<SkillData> allSkills;

        public SkillData GetSkillById(string id)
        {
            return allSkills.Find(skill => skill.skillID == id);
        }
        public SkillData GetSkillByType(Nytherion.Core.Enums.SkillType type)
        {
            return allSkills.Find(skill => skill.skillType == type);
        }
    }
}