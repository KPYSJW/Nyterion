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
            return allSkills.Find(skill => skill.name == id);
        }
    }
}