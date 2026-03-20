using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "Data/Skill")]
    public class SkillData : ScriptableObject
    {
        [Header("Skill ID")]
        public string skillID;
        
        [Header("Skill Type")]
        public SkillType skillType;

        [Header("Skill Info")]
        public string skillName;
        public float coolDown;

        
        [Header("Skill Icon")]
        public Sprite icon;

        [Header("Skill Prefab")]
        public GameObject skillPrefab;
    }
}
