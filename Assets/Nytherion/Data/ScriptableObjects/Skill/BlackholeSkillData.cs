using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "NewBlackholeData", menuName = "Data/Skill/Blackhole")]
    public class BlackholeSkillData : SkillData
    {
        [Header("Blackhole Specific Settings")]
        public float pullForce = 15f;      
        public float duration = 4f;        
        public float tickRate = 0.5f;      
        public LayerMask enemyLayer;       
    }
}