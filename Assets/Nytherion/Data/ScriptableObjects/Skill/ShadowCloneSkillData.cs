using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "NewShadowCloneData", menuName = "Data/Skill/ShadowClone")]
    public class ShadowCloneSkillData : SkillData
    {
        /// <summary>
        /// 분신 스킬 데이터 저장 ScriptableObject
        /// </summary>
        [Header("Shadow Clone Settings")]
        [Tooltip("지속 시간")]
        public float duration = 10f;
        [Tooltip("기본 데미지 비율 (예: 0.3 = 30%)")]
        public float baseDamageRatio = 0.3f;
        [Tooltip("스킬 레벨업 당 증가하는 데미지 비율")]
        public float damageRatioPerLevel = 0.05f;
    }
}
