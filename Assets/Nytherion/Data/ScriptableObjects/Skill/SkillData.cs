using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "Data/Skill")]
    public class SkillData : ScriptableObject
    {
        public string skillName;
        public float coolDown;
        public Sprite icon;
        public GameObject skillPrefab;
    }
}
