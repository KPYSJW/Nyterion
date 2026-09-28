using UnityEngine;
using UnityEngine.Serialization;

namespace Nytherion.Data.ScriptableObjects.Weapons
{
    [CreateAssetMenu(fileName = "VoidRay", menuName = "Nytherion/Weapons/Void Ray")]
    public class VoidRayWeaponData : WeaponData
    {
        [Header("지속 광선 피해")]
        [Min(0f)] public float damagePerSecond = 5f;
        [Min(0.01f)] public float damageInterval = 0.2f;
        [Min(3), Tooltip("첫 명중 대상을 포함해 연쇄 광선으로 공격할 최대 적 수입니다.")]
        public int maxTargetCount = 3;
        [Min(0.1f), Tooltip("현재 명중한 적의 중심에서 다음 적을 탐색할 반경입니다.")]
        public float chainRange = 3f;
        public LayerMask targetLayers;
        public LayerMask obstructionLayers;

        [Header("허공 방전 (적에게 닿지 않았을 때)")]
        [Min(0.01f)] public float airOuterWidth = 0.09375f;
        [Min(0.01f)] public float airCoreWidth = 0.0625f;
        [Min(0f), Tooltip("허공에서는 가는 번개가 넓게 움직입니다. 충돌 판정에는 영향을 주지 않습니다.")]
        public float airJitterStrength = 0.45f;
        [Range(8, 32), Tooltip("허공 광선의 곡선과 작은 꺾임을 표현하는 마디 수입니다.")]
        public int airVisualSegments = 24;
        [Min(0.01f), Tooltip("허공 번개가 새로운 불규칙 경로로 바뀌는 간격입니다.")]
        public float airPathRefreshInterval = 0.05f;
        [Min(0f), Tooltip("허공 광선 끝점이 최대 사거리 안쪽으로 움직이는 최대 거리입니다.")]
        public float airEndpointLengthJitter = 0.65f;
        [Min(0f), Tooltip("허공 광선 끝점이 조준선 좌우로 움직이는 최대 거리입니다.")]
        public float airEndpointSideJitter = 0.28f;
        [Range(0f, 1f), Tooltip("허공 광선 끝부분의 투명도 배율입니다. 0이면 완전히 사라집니다.")]
        public float airTipAlpha = 0.12f;
        [Range(0, 3), Tooltip("허공에서 주 광선 일부가 잠깐 갈라져 보이는 가는 보조 궤적 수입니다.")]
        public int airStrandCount = 2;
        [Range(0.1f, 1f), Tooltip("허공의 짧은 보조 궤적 두께 배율입니다.")]
        public float airStrandWidthMultiplier = 0.42f;
        [Range(0f, 1f), Tooltip("허공의 짧은 보조 궤적 투명도입니다.")]
        public float airStrandAlpha = 0.34f;
        [Range(0.25f, 2f), Tooltip("허공의 짧은 보조 궤적이 주 광선에서 벌어지는 정도입니다.")]
        public float airStrandSpreadMultiplier = 1.15f;
        [Min(0f), Tooltip("허공 광선의 무늬와 짧은 에너지 조각이 총구에서 끝 방향으로 흐르는 속도입니다.")]
        public float airFlowSpeed = 1.6f;
        [Min(0f), Tooltip("회전한 총구 방향에서 광선 경로로 이어지는 첫 구간 길이입니다.")]
        public float muzzleBendDistance = 0.5f;

        [Header("적 연결 광선 (명중 중에만 굵어짐)")]
        [Min(0.01f)] public float outerWidth = 0.36f;
        [Min(0.01f)] public float coreWidth = 0.09375f;
        public Color outerColor = new Color(0.02f, 0.8f, 0.95f, 0.9f);
        public Color coreColor = new Color(0.88f, 1f, 1f, 1f);
        [Range(2, 6)] public int connectedStrandCount = 5;
        [Range(0.25f, 2f)] public float connectedStrandSpreadMultiplier = 1.2f;
        [Range(0f, 1f)] public float connectedGlowAlpha = 0f;
        [Range(0f, 1f)] public float damagePulseStrength = 0.3f;
        [Min(0.01f), Tooltip("실제 피해 틱 이후 굵기와 명중점의 밝기가 가라앉는 시간입니다.")]
        public float damagePulseDuration = 0.07f;
        [Tooltip("광선이 몬스터 스프라이트보다 앞에 표시될 기준 정렬 순서입니다.")]
        public int beamSortingOrder = 30;

        [Header("번개 움직임 (시각 효과 전용)")]
        [Range(1, 8), Tooltip("VoidRayEffect 가로 스프라이트 시트의 32x32 프레임 수입니다.")]
        public int bodyFrameCount = 4;
        [Min(0f)] public float bodyAnimationSpeed = 14f;
        [Min(0f)] public float jitterStrength = 0.24f;
        [Range(4, 24)] public int visualSegments = 12;
        [Min(0.01f), Tooltip("번개 경로가 다음 형태로 바뀌는 간격입니다.")]
        public float pathRefreshInterval = 0.045f;
        [Range(0f, 1f)] public float secondaryArcAlpha = 0.78f;
        [Range(0.1f, 1f)] public float secondaryArcWidthMultiplier = 0.7f;

        [Header("명중 불똥 (피해 틱과 동기화, 이미지 미사용)")]
        [Range(0, 8)] public int impactSparkCount = 8;
        [Min(0f), Tooltip("사각형 불똥이 적 중심에서 이동하는 거리입니다.")]
        public float impactSparkLength = 0.48f;
        [Min(0.005f), Tooltip("사각형 불똥 한 조각의 크기입니다.")]
        public float impactSparkWidth = 0.078125f;
        [Min(0.01f), Tooltip("불똥이 튀었다가 사라지는 시간입니다.")]
        public float impactSparkDuration = 0.22f;
        [Tooltip("청록색 광선과 구분되는 용접 불똥 색상입니다.")]
        public Color impactSparkColor = new Color(1f, 0.62f, 0.08f, 1f);
        [FormerlySerializedAs("hitEffectSortingOrder")]
        [Tooltip("사각형 불똥이 몬스터보다 앞에 표시될 정렬 순서입니다.")]
        public int impactSparkSortingOrder = 34;

        [Header("시작 효과")]
        [Tooltip("발사 시작점에서 반복 재생할 Point 필터 스프라이트입니다.")]
        public Sprite[] startEndFrames = new Sprite[6];
        [Min(0.1f), Tooltip("VoidRayStart 크기 배율입니다.")]
        public float startEffectSizeMultiplier = 4f;
        [Min(0f)] public float visualFadeDuration = 0.08f;

        public float MaxRange => Mathf.Max(0.01f, range);
        public float DamageInterval => Mathf.Max(0.01f, damageInterval);
        public float DamagePerTick => Mathf.Max(0f, damagePerSecond) * DamageInterval;
        public int MaxTargetCount => Mathf.Max(3, maxTargetCount);
        public float ChainRange => Mathf.Max(0.1f, chainRange);
        public float OuterWidth => Mathf.Max(0.01f, outerWidth);
        public float CoreWidth => Mathf.Clamp(coreWidth, 0.01f, OuterWidth);
        public float AirOuterWidth => Mathf.Clamp(airOuterWidth, 0.01f, OuterWidth);
        public float AirCoreWidth => Mathf.Clamp(airCoreWidth, 0.01f, AirOuterWidth);
        public float AirTipAlpha => Mathf.Clamp01(airTipAlpha);
        public int AirStrandCount => Mathf.Clamp(airStrandCount, 0, 3);
        public float AirFlowSpeed => Mathf.Max(0f, airFlowSpeed);
        public int ConnectedStrandCount => Mathf.Clamp(connectedStrandCount, 2, 6);
        public int ImpactSparkCount => Mathf.Clamp(impactSparkCount, 0, 8);
        public int AirVisualSegments => Mathf.Clamp(airVisualSegments, 8, 32);
        public int VisualSegments => Mathf.Clamp(visualSegments, 4, 24);
        public int BodyFrameCount => Mathf.Clamp(bodyFrameCount, 1, 8);
        public float AirPathRefreshInterval => Mathf.Max(0.01f, airPathRefreshInterval);
        public float PathRefreshInterval => Mathf.Max(0.01f, pathRefreshInterval);
        public bool HasStartFrames => startEndFrames != null && startEndFrames.Length > 0 &&
            System.Array.TrueForAll(startEndFrames, frame => frame != null);
    }
}
