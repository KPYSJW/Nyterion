using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "FireStorm_Skill", menuName = "Data/Skill/Fire Storm")]
    public sealed class FireStormSkillData : SkillData
    {
        [Header("화염 폭풍")]
        [Min(0.01f)] public float duration = 5f;
        [Min(0.01f)] public float tickInterval = 0.5f;
        [Tooltip("바닥 타격 타원의 가로 반경입니다.")]
        [Min(0.01f)] public float radius = 0.75f;
        [Tooltip("바닥 타격 타원의 세로 반경입니다.")]
        [Min(0.01f)] public float verticalRadius = 0.2f;
        public Vector2 damageOffset = new Vector2(0f, -0.1f);
        [Min(0f)] public float damagePerLevel = 2f;
        public LayerMask targetLayers;
        [Min(0.01f)] public float burnDuration = 5f;
        [Min(0f)] public float burnDamageRatio = 0.2f;

        [Header("애니메이션")]
        public Sprite[] spawnFrames;
        public Sprite[] loopFrames;
        public Sprite[] hitFrames;
        [Min(1f)] public float spawnFps = 12f;
        [Min(1f)] public float loopFps = 12f;
        [Min(1f)] public float hitFps = 14f;
        [Min(0.01f)] public float visualScale = 1.5f;
        [Min(0.01f)] public float hitVisualScale = 1f;

        public float GetDamage(int level) => Mathf.Max(0f, damage + damagePerLevel * Mathf.Max(0, level - 1));
        public bool HasValidFrames => Valid(spawnFrames, 8) && Valid(loopFrames, 16) && Valid(hitFrames, 7);

        private static bool Valid(Sprite[] frames, int count)
        {
            if (frames == null || frames.Length != count) return false;
            foreach (Sprite frame in frames) if (frame == null) return false;
            return true;
        }
    }
}
