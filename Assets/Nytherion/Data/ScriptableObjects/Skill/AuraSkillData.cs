using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "NewAuraData", menuName = "Data/Skill/Aura")]
    public class AuraSkillData : SkillData
    {
        [Header("Aura Settings")]
        [Tooltip("오라 스킬 지속 시간")]
        public float auraDuration = 5f;
        [Tooltip("오라의 반경 크기")]
        public float auraRadius = 3f;

        [Header("Damage Settings")]
        [Tooltip("범위 내 적에게 지속 데미지를 줄지 여부")]
        public bool dealDamage = true;
        [Tooltip("1회 타격당 기본 데미지")]
        public float damagePerTick = 10f;
        [Tooltip("스킬 레벨업 당 증가하는 타격 데미지")]
        public float damagePerLevel = 2f;
        [Tooltip("데미지를 주는 간격 (초)")]
        public float tickRate = 0.5f;

        [Header("Utility Settings")]
        [Tooltip("범위 내로 들어오는 적의 원거리 투사체를 파괴할지 여부")]
        public bool destroyEnemyProjectiles = true;
    }
}
