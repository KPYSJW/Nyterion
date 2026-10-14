using UnityEngine;
using Nytherion.GamePlay.Skills;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "ChainIgnition_Skill", menuName = "Data/Skill/Chain Ignition")]
    public class ChainIgnitionSkillData : SkillData
    {
        public const int WaveCount = 3;
        public const int DirectionCount = 8;

        [Header("시전 중심 보정")]
        [Tooltip("플레이어 위치에 더하는 폭발들의 바닥 중심 보정입니다. 원 전체와 조준 기준점이 함께 이동합니다. 불꽃 이미지만 옮길 때는 Explosion Visual Offset을 사용하세요.")]
        public Vector2 castCenterOffset = Vector2.zero;

        [Header("레벨 성장과 폭발 방향")]
        [Range(1, DirectionCount)] public int baseProjectileCount = 1;
        [Tooltip("1레벨을 기준으로 이 레벨 간격마다 폭발 방향이 하나 늘어납니다. 2이면 1/3/5/7/9레벨에 증가합니다.")]
        [Min(1)] public int levelsPerProjectile = 2;
        [Min(0f)] public float damagePerLevel = 2f;
        [Tooltip("마우스 방향과의 각도 차이가 이 값 이내로 비슷하면 해당 방향들 중 무작위로 고릅니다.")]
        [Range(0f, 22.5f)] public float directionTieAngle = 2f;

        [Header("연쇄 폭발 설정")]
        [Tooltip("첫 폭발 원의 반경입니다. 마지막 원의 반경은 기본 사거리(range)를 사용합니다.")]
        [Min(0.01f)] public float firstRingRadius = 1f;
        [Tooltip("폭발 시작 사이의 간격입니다. 애니메이션 길이보다 짧게 설정해도 이전 폭발이 끝난 뒤 다음 폭발이 시작됩니다.")]
        [Min(0.01f)] public float waveInterval = 0.2f;
        [Tooltip("각 폭발의 피해 반경입니다. 타원을 사용하면 가로 반경으로 사용합니다. 불꽃 이미지의 높이는 판정에 영향을 주지 않습니다.")]
        [Min(0.01f)] public float explosionRadius = 0.6f;
        [Tooltip("가로·세로 반경을 따로 설정하는 타원 판정을 사용합니다. 끄면 기존 원형 판정을 사용합니다.")]
        public bool useEllipticalHitRange;
        [Min(0.01f)] public float explosionVerticalRadius = 0.3f;
        public LayerMask targetLayers;

        [Header("폭발 바닥과 이미지 정렬")]
        [Tooltip("폭발의 바닥 중심에서 이미지 기준점까지의 스프라이트 좌표 보정입니다. Y를 올리면 불꽃 이미지만 올라가며 Visual Scale과 크기 증가 유물에 함께 비례합니다.")]
        public Vector2 explosionVisualOffset = new Vector2(0f, 0.35f);

        [Header("폭발 애니메이션")]
        public Sprite[] explosionFrames;
        [Min(1f)] public float framesPerSecond = 12f;
        [Min(0.01f)] public float visualScale = 1.6f;
        [Tooltip("피해를 적용하는 애니메이션 프레임입니다. 0부터 시작합니다.")]
        [Min(0)] public int damageFrame = 1;
        public ChainIgnitionWave wavePrefab;

        [Header("폭발 효과음")]
        [Tooltip("각 원의 선택된 방향들이 동시에 폭발할 때 한 번씩 재생합니다.")]
        public AudioClip explosionSound;
        [Range(0f, 1f)] public float explosionSoundVolume = 0.5f;

        [Header("피해 범위 확인")]
        [Tooltip("게임 화면에 각 폭발의 실제 원형 또는 타원형 피해 범위를 표시합니다. 피해는 Damage Frame에서 한 번 적용됩니다.")]
        public bool showHitRanges;
        public Color hitRangeColor = new Color(0f, 1f, 1f, 0.85f);
        [Min(0.001f)] public float hitRangeLineWidth = 0.03f;

        public int GetProjectileCount(int level, float extraProjectiles = 0f)
        {
            int levelBonus = Mathf.Max(0, level - 1) / Mathf.Max(1, levelsPerProjectile);
            return Mathf.Clamp(baseProjectileCount + levelBonus + Mathf.FloorToInt(extraProjectiles), 1, DirectionCount);
        }

        public float GetDamage(int level) => Mathf.Max(0f, damage + damagePerLevel * Mathf.Max(0, level - 1));

        public static float GetSizeRangeBonus(float sizeMultiplier) => Mathf.Max(0f, sizeMultiplier - 1f) * 0.5f;

        /// <summary>크기 증가분의 절반을 연쇄 점화의 확산 거리 배율에 더합니다.</summary>
        public static float GetSpreadRangeMultiplier(float sizeMultiplier)
        {
            return 1f + GetSizeRangeBonus(sizeMultiplier);
        }

        public Vector2 GetExplosionRadii(float sizeMultiplier = 1f)
        {
            float size = Mathf.Max(0.01f, sizeMultiplier);
            return new Vector2(Mathf.Max(0.01f, explosionRadius * size),
                Mathf.Max(0.01f, (useEllipticalHitRange ? explosionVerticalRadius : explosionRadius) * size));
        }

        public float AnimationDuration => (explosionFrames?.Length ?? 0) / Mathf.Max(1f, framesPerSecond);
        public float WaveInterval => Mathf.Max(AnimationDuration, Mathf.Max(0.01f, waveInterval));
        public float DamageDelay => Mathf.Clamp(damageFrame, 0, (explosionFrames?.Length ?? 1) - 1) /
                                    Mathf.Max(1f, framesPerSecond);

        public float GetRingRadius(int waveIndex)
        {
            float innerRadius = Mathf.Max(0.01f, firstRingRadius);
            return Mathf.Lerp(innerRadius, Mathf.Max(innerRadius, range),
                Mathf.Clamp(waveIndex, 0, WaveCount - 1) / (float)(WaveCount - 1));
        }

        public bool HasValidAnimation
        {
            get
            {
                if (explosionFrames == null || explosionFrames.Length == 0) return false;
                foreach (Sprite frame in explosionFrames)
                    if (frame == null) return false;
                return true;
            }
        }
    }
}
