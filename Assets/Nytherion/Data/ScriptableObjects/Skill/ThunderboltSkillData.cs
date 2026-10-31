using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "Thunderbolt_Skill", menuName = "Data/Skill/Thunderbolt")]
    public class ThunderboltSkillData : SkillData
    {
        public ThunderboltProjectile projectilePrefab;
        public Sprite[] projectileFrames;
        [Header("타격 이펙트")]
        public Sprite[] hitFrames;
        [Min(1f)] public float hitFps = 14f;
        [Min(0.01f)] public float hitVisualScale = 0.5f;
        [Min(0.01f)] public float hitVerticalScale = 1f;
        [Min(0.01f)] public float speed = 8f;
        [Min(0.01f)] public float projectileLifetime = 1f;
        [Min(0.01f)] public float collisionRadius = 0.25f;
        [Min(0.01f)] public float visualScale = 0.6f;
        [Range(0f, 180f)] public float projectileSpreadAngle = 60f;
        [Min(1f)] public float projectileFps = 12f;
        [Min(0f)] public float damagePerLevel = 2f;
        public LayerMask collisionLayers;
        public float GetDamage(int level) => Mathf.Max(0f, damage + damagePerLevel * Mathf.Max(0, level - 1));
        public bool HasValidFrames => projectileFrames != null && projectileFrames.Length > 0 && System.Array.TrueForAll(projectileFrames, frame => frame != null) &&
            hitFrames != null && hitFrames.Length > 0 && System.Array.TrueForAll(hitFrames, frame => frame != null);
    }
}
