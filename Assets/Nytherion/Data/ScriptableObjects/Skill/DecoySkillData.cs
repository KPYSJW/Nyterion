using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "Decoy_Skill", menuName = "Data/Skill/Decoy")]
    public class DecoySkillData : SkillData
    {
        [Header("더미 배치 및 유인")]
        [Min(0.1f)] public float spawnDistance = 1f;
        [Min(0.1f)] public float collisionRadius = 0.22f;
        [Range(2f, 3f)] public float tauntRadius = 2.5f;
        public LayerMask blockingLayers = (1 << 9) | (1 << 11);

        [Header("레벨별 내구도 및 지속시간")]
        [Min(1f)] public float baseDurability = 100f;
        [Min(0f)] public float durabilityPerLevel = 25f;
        [Min(0.1f)] public float baseLifetime = 8f;
        [Min(0f)] public float lifetimePerLevel = 1f;

        [Header("소환 연출")]
        public DecoyDummy dummyPrefab;
        public Sprite[] summonFrames;
        [Min(1f)] public float framesPerSecond = 10f;
        public Sprite dummySprite;

        public float GetDurability(int level) => baseDurability + durabilityPerLevel * Mathf.Max(0, level - 1);
        public float GetLifetime(int level) => baseLifetime + lifetimePerLevel * Mathf.Max(0, level - 1);
    }
}
