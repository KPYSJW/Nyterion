using UnityEngine;
using Nytherion.Core.Enums;

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
        [TextArea(3, 5)] 
        public string description;
        public float coolDown;
        public float damage;
        public float range;

        [Header("Skill Icon")]
        public Sprite icon;

        [Header("Skill Prefab")]
        public GameObject skillPrefab;

    }
}
