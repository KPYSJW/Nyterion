using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "Fireball_Skill", menuName = "Data/Skill/Fireball")]
    public class FireballSkillData : SkillData
    {
        [Header("투사체")]
        public FireballProjectile projectilePrefab;
        public Sprite[] projectileFrames;
        public Sprite[] hitFrames;
        [Tooltip("투사체 이미지의 기본 크기입니다. 충돌 반경에는 영향을 주지 않습니다.")]
        [Min(0.01f)] public float projectileVisualScale = 0.6f;
        [Tooltip("타격 불꽃 이미지의 기본 크기입니다.")]
        [Min(0.01f)] public float hitVisualScale = 0.5f;
        [Tooltip("타격 불꽃 이미지의 세로 비율입니다. 1이면 원본 비율을 유지합니다.")]
        [Min(0.01f)] public float hitVerticalScale = 1f;
        [Min(0.01f)] public float speed = 8f;
        [Tooltip("투사체가 날아가는 시간입니다. 시간이 끝나면 그 자리에서 이펙트를 재생합니다.")]
        [Min(0.01f)] public float projectileLifetime = 1f;
        [Tooltip("여러 투사체를 발사할 때 양 끝 투사체 사이의 전체 각도입니다.")]
        [Range(0f, 180f)] public float projectileSpreadAngle = 60f;
        [Min(0.01f)] public float collisionRadius = 0.25f;
        [Min(1f)] public float projectileFps = 12f;
        [Min(1f)] public float hitFps = 14f;
        [Min(0f)] public float damagePerLevel = 2f;
        public LayerMask collisionLayers;

        public float GetDamage(int level) => Mathf.Max(0f, damage + damagePerLevel * Mathf.Max(0, level - 1));
        public bool HasValidFrames => projectileFrames != null && projectileFrames.Length > 0 &&
            hitFrames != null && hitFrames.Length > 0;
    }
}
