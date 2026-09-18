using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Weapons
{
    [CreateAssetMenu(fileName = "LayLaser", menuName = "Nytherion/Weapons/Lay Laser")]
    public class LayLaserWeaponData : WeaponData
    {
        public const int ChargeStartupFrameCount = 12;
        public const int FullChargeStartFrame = 12;
        public const int FullChargeFrameCount = 7;
        public const float ChargeAnimationFramesPerSecond = 10f;
        public const float ChargeAnimationDuration = ChargeStartupFrameCount / ChargeAnimationFramesPerSecond;

        [Header("4단계 차징 (0~3 / 4~7 / 8~11 / 12~18)")]
        public Sprite[] chargeFrames = new Sprite[19];
        [Min(1f)] public float fullChargeFramesPerSecond = 10f;

        [Header("단계별 광선 폭 (1 / 2 / 3 / 4단계)")]
        public Vector4 widthMultipliers = new Vector4(0.5f, 1f, 1.7f, 2.6f);
        [Min(0.01f)] public float beamWidth = 0.5f;
        [Tooltip("4단계 총구 위치를 기준으로 낮은 단계의 작은 시작 효과를 무기 쪽으로 당깁니다.")]
        public Vector4 firePointStageMultipliers = new Vector4(0.72f, 0.78f, 0.87f, 1f);

        [Header("광선 애니메이션 및 틱 피해")]
        [Tooltip("원본 순서대로 0~7 프레임을 한 번만 재생합니다.")]
        public Sprite[] beamFrames = new Sprite[8];
        [Tooltip("LayLaserEffect와 같은 인덱스로 시작점과 끝점에 재생합니다.")]
        public Sprite[] startEndFrames = new Sprite[8];
        [Min(1f)] public float beamFramesPerSecond = 16f;
        [Min(0.01f)] public float tickInterval = 0.2f;
        [Tooltip("원본 프레임에서 광선의 최대 불투명 폭 / 프레임 전체 폭입니다.")]
        [Range(0.01f, 1f)] public float beamOpaqueWidthRatio = 0.625f;
        public LayerMask targetLayers;
        public LayerMask obstructionLayers;

        public int BeamSequenceLength => beamFrames == null ? 0 : beamFrames.Length;
        public float BeamDuration => BeamSequenceLength / Mathf.Max(1f, beamFramesPerSecond);
        public float DamageStartTime => 2f / Mathf.Max(1f, beamFramesPerSecond);
        public float DamageEndTime => Mathf.Max(DamageStartTime,
            (BeamSequenceLength - 2f) / Mathf.Max(1f, beamFramesPerSecond));

        public int GetBeamFrameIndex(int sequenceFrame)
        {
            return Mathf.Clamp(sequenceFrame, 0, Mathf.Max(0, BeamSequenceLength - 1));
        }

        public int GetChargeStage(float chargePercent)
        {
            return Mathf.Min(3, Mathf.FloorToInt(Mathf.Clamp01(chargePercent) * 3f + 0.00001f));
        }

        public float GetLength(int stage) => Mathf.Max(0.01f, range);
        public float GetWidth(int stage) => Mathf.Max(0.01f, beamWidth * widthMultipliers[Mathf.Clamp(stage, 0, 3)]);
        public Vector3 GetFirePointOffset(int stage)
        {
            Vector3 offset = firePointOffset;
            offset.x *= Mathf.Max(0f, firePointStageMultipliers[Mathf.Clamp(stage, 0, 3)]);
            return offset;
        }

        public bool HasValidFrames => chargeFrames != null && chargeFrames.Length == 19 &&
            beamFrames != null && beamFrames.Length == 8 &&
            startEndFrames != null && startEndFrames.Length == beamFrames.Length &&
            System.Array.TrueForAll(chargeFrames, frame => frame != null) &&
            System.Array.TrueForAll(beamFrames, frame => frame != null) &&
            System.Array.TrueForAll(startEndFrames, frame => frame != null);
    }
}
