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
            if (allSkills == null) return null;
            return allSkills.Find(skill => skill != null && skill.skillID == id);
        }
        public SkillData GetSkillByType(Nytherion.Core.Enums.SkillType type)
        {
            if (allSkills == null) return null;
            return allSkills.Find(skill => skill != null && skill.skillType == type);
        }
    }
}