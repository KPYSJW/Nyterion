using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    // 기존 에셋의 GUID를 보존하기 위해 파일명과 클래스명은 유지합니다.
    [CreateAssetMenu(fileName = "DarkCloud_Summon", menuName = "Data/Relic/Dark Cloud Summon")]
    public class DarkCloudSkillData : ScriptableObject
    {
        [Header("먹구름")]
        [UnityEngine.Serialization.FormerlySerializedAs("skillPrefab")]
        public GameObject summonPrefab;
        [Min(0f)] public float damage = 10f;
        [Min(0f)] public float damagePerLevel = 2f;
        [Min(0.01f)] public float attackInterval = 5f;
        [Min(0.01f)] public float detectionRadius = 6f;
        [Min(0.01f)] public float cloudHeight = 2f;
        [Min(0.01f)] public float moveSpeed = 5f;
        [Min(0.01f)] public float impactRadius = 1f;
        [Min(0.01f)] public float shockDuration = 5f;
        public LayerMask targetLayers;

        [Header("애니메이션")]
        public Sprite[] idleFrames;
        public Sprite[] boltFrames;
        public Sprite[] impactFrames;
        public Sprite[] launchCloudFrames;
        [Min(0.01f)] public float idleFps = 8f;
        [Min(0.01f)] public float impactFps = 12f;
        [Min(0.01f)] public float launchFps = 12f;
        [Min(0.01f)] public float cloudVisualScale = 0.9375f;

        public float GetDamage(int level) => Mathf.Max(0f, damage + Mathf.Max(0f, damagePerLevel) * Mathf.Max(0, level - 1));

        public bool HasValidFrames => Valid(idleFrames) && Valid(impactFrames) &&
            Valid(boltFrames) && boltFrames.Length == 4 &&
            Valid(launchCloudFrames) && launchCloudFrames.Length == 10;
        private static bool Valid(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return false;
            foreach (Sprite frame in frames)
                if (frame == null) return false;
            return true;
        }
    }
}
