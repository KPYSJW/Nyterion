using UnityEngine;
using Nytherion.GamePlay.Skills;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "BubbleShield_Skill", menuName = "Data/Skill/Bubble Shield")]
    public class BubbleShieldSkillData : SkillData
    {
        [Header("실드량 및 지속시간")]
        [Min(1f)] public float baseShield = 100f;
        [Min(0f)] public float shieldPerLevel = 25f;
        [Min(0.1f)] public float duration = 8f;

        [Header("생성 · 유지 · 파괴 연출")]
        public Sprite[] summonFrames;
        public Sprite shieldSprite;
        public Sprite[] crashFrames;
        [Min(1f)] public float framesPerSecond = 12f;
        [Min(0.1f)] public float sizeMultiplier = 1.5f;
        public Color shieldColor = new Color(0.65f, 0.9f, 1f, 0.38f);

        [Header("피격 연출")]
        public BubbleShieldHitEffect hitEffectPrefab;
        [Min(0.1f)] public float hitEffectScale = 1f;
        [Tooltip("프레임 너비 대비 실드 표면 반지름. 기본 이미지의 원은 32픽셀 중 반지름 11픽셀입니다.")]
        [Range(0.1f, 0.5f)] public float hitSurfaceRadius = 0.34375f;

        [Header("실드 바")]
        public Sprite shieldBarSprite;

        public float GetShield(int level) => baseShield + shieldPerLevel * Mathf.Max(0, level - 1);
    }
}
