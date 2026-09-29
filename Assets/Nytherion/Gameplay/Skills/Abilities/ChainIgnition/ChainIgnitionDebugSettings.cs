using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>플레이어별 임시 테스트 값입니다. 원본 스킬 데이터와 저장 상태는 수정하지 않습니다.</summary>
    public class ChainIgnitionDebugSettings : MonoBehaviour
    {
        public bool useTestSettings;
        public bool overrideProjectileCount;
        [Range(1, 8)] public int projectileCount = 1;
        public float sizeMultiplier = 1f;
        [Tooltip("크기 증가에 따른 거리 보정을 더하기 전의 테스트 배율입니다. 디버그 UI에는 보정을 포함한 배율을 표시합니다.")]
        public float rangeMultiplier = 1f;
        public float firstRingRadius;
        public float lastRingRadius;
        public float horizontalRadius;
        public float verticalRadius;
        public float visualScale;
        public Vector2 centerOffset;
        public Vector2 visualOffset;
        public bool useEllipse;
        public bool showHitRanges;
        public float soundVolume;

        public void ResetToDefaults(ChainIgnitionSkillData source)
        {
            useTestSettings = false;
            overrideProjectileCount = false;
            projectileCount = source.baseProjectileCount;
            sizeMultiplier = rangeMultiplier = 1f;
            firstRingRadius = source.firstRingRadius;
            lastRingRadius = source.range;
            horizontalRadius = source.explosionRadius;
            verticalRadius = source.explosionVerticalRadius;
            visualScale = source.visualScale;
            centerOffset = source.castCenterOffset;
            visualOffset = source.explosionVisualOffset;
            useEllipse = source.useEllipticalHitRange;
            showHitRanges = source.showHitRanges;
            soundVolume = source.explosionSoundVolume;
        }

        public int GetProjectileCount(ChainIgnitionSkillData source, int level, float extraProjectiles)
        {
            return useTestSettings && overrideProjectileCount
                ? Mathf.Clamp(projectileCount, 1, ChainIgnitionSkillData.DirectionCount)
                : source.GetProjectileCount(level, extraProjectiles);
        }

        public ChainIgnitionSkillData CreateCastData(ChainIgnitionSkillData source)
        {
            // 파동마다 복사하여 조절 중인 값이 진행 중인 폭발의 판정·타이밍에 섞이지 않게 합니다.
            ChainIgnitionSkillData copy = Instantiate(source);
            copy.name = source.name + " (디버그 시전)";
            copy.hideFlags = HideFlags.DontSave;
            copy.firstRingRadius = Mathf.Max(0.01f, firstRingRadius);
            copy.range = Mathf.Max(copy.firstRingRadius, lastRingRadius);
            copy.explosionRadius = Mathf.Max(0.01f, horizontalRadius);
            copy.explosionVerticalRadius = Mathf.Max(0.01f, verticalRadius);
            copy.visualScale = Mathf.Max(0.01f, visualScale);
            copy.castCenterOffset = centerOffset;
            copy.explosionVisualOffset = visualOffset;
            copy.useEllipticalHitRange = useEllipse;
            copy.showHitRanges = showHitRanges;
            copy.explosionSoundVolume = Mathf.Clamp01(soundVolume);
            return copy;
        }
    }
}
