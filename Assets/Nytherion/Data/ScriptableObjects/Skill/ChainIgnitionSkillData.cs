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
        [Tooltip("플레이어 위치에 더하는 월드 좌표 보정입니다. X는 좌우, Y는 상하이며 폭발 연출과 피해 판정이 함께 이동합니다.")]
        public Vector2 castCenterOffset = Vector2.zero;

        [Header("연쇄 폭발 설정")]
        [Tooltip("첫 폭발 원의 반경입니다. 마지막 원의 반경은 기본 사거리(range)를 사용합니다.")]
        [Min(0.01f)] public float firstRingRadius = 1.2f;
        [Tooltip("폭발 시작 사이의 간격입니다. 애니메이션 길이보다 짧게 설정해도 이전 폭발이 끝난 뒤 다음 폭발이 시작됩니다.")]
        [Min(0.01f)] public float waveInterval = 0.2f;
        [Min(0.01f)] public float explosionRadius = 0.9f;
        public LayerMask targetLayers;

        [Header("폭발 애니메이션")]
        public Sprite[] explosionFrames;
        [Min(1f)] public float framesPerSecond = 12f;
        [Min(0.01f)] public float visualScale = 2.4f;
        [Tooltip("피해를 적용하는 애니메이션 프레임입니다. 0부터 시작합니다.")]
        [Min(0)] public int damageFrame = 1;
        public ChainIgnitionWave wavePrefab;

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
