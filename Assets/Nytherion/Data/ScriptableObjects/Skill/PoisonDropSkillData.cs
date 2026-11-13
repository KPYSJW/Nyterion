using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "PoisonDrop_Skill", menuName = "Data/Skill/Poison Drop")]
    public sealed class PoisonDropSkillData : SkillData
    {
        [Header("독방울")]
        [Min(0.01f)] public float fallDuration = 0.35f;
        [Min(0.01f)] public float fallHeight = 4f;
        [Min(0.01f)] public float radius = 1.1f;
        [Min(0.01f)] public float verticalRadius = 0.4f;
        public LayerMask targetLayers;
        [Min(0f)] public float damagePerLevel = 3f;
        [Min(0f)] public float poisonDamageRatio = 0.2f;
        [Min(0.01f)] public float poisonDuration = 5f;

        [Header("애니메이션")]
        public Sprite dropSprite;
        [Tooltip("왼쪽 위부터 행 순서로 착탄·웅덩이·소멸 13프레임을 한 번 재생합니다.")]
        public Sprite[] frames;
        [Min(1f)] public float animationFps = 12f;
        [Min(0.01f)] public float visualScale = 1f;

        public float GetDamage(int level) => Mathf.Max(0f, damage + damagePerLevel * Mathf.Max(0, level - 1));
        public bool HasValidFrames
        {
            get
            {
                if (dropSprite == null || frames == null || frames.Length != 13) return false;
                foreach (Sprite frame in frames) if (frame == null) return false;
                return true;
            }
        }
    }
}
