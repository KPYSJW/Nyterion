using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "NewLaserData", menuName = "Data/Skill/Laser")]
    public class LaserSkillData : SkillData
    {
        [Header("Laser Specific Settings")]
        public float fireDuration = 2f;
        public float tickRate = 0.2f;
    }
}