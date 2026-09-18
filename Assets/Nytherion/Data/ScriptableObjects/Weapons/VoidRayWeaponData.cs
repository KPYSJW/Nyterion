using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Weapons
{
    [CreateAssetMenu(fileName = "VoidRay", menuName = "Nytherion/Weapons/Void Ray")]
    public class VoidRayWeaponData : WeaponData
    {
        [Header("지속 광선 피해")]
        [Min(0f)] public float damagePerSecond = 30f;
        [Min(0.01f)] public float damageInterval = 0.1f;
        public LayerMask targetLayers;
        public LayerMask obstructionLayers;

        [Header("허공 방전 (적에게 닿지 않았을 때)")]
        [Min(0.01f)] public float airOuterWidth = 0.09375f;
        [Min(0.01f)] public float airCoreWidth = 0.0625f;
        [Min(0f), Tooltip("허공에서는 가는 번개가 넓게 움직입니다. 충돌 판정에는 영향을 주지 않습니다.")]
        public float airJitterStrength = 0.32f;

        [Header("적 연결 광선 (명중 중에만 굵어짐)")]
        [Min(0.01f)] public float outerWidth = 0.36f;
        [Min(0.01f)] public float coreWidth = 0.09375f;
        public Color outerColor = new Color(0.02f, 0.8f, 0.95f, 0.9f);
        public Color coreColor = new Color(0.88f, 1f, 1f, 1f);
        [Range(2, 6)] public int connectedStrandCount = 5;
        [Range(0.25f, 2f)] public float connectedStrandSpreadMultiplier = 1.2f;
        [Range(0f, 1f)] public float connectedGlowAlpha = 0.24f;
        [Range(0f, 1f)] public float damagePulseStrength = 0.3f;
        [Min(0.01f), Tooltip("실제 피해 틱 이후 굵기와 명중점의 밝기가 가라앉는 시간입니다.")]
        public float damagePulseDuration = 0.07f;

        [Header("번개 움직임 (시각 효과 전용)")]
        [Min(0f)] public float bodyAnimationSpeed = 14f;
        [Min(0f)] public float jitterStrength = 0.24f;
        [Range(4, 24)] public int visualSegments = 12;
        [Min(0.01f), Tooltip("번개 경로가 다음 형태로 바뀌는 간격입니다.")]
        public float pathRefreshInterval = 0.045f;
        [Range(0f, 1f)] public float secondaryArcAlpha = 0.78f;
        [Range(0.1f, 1f)] public float secondaryArcWidthMultiplier = 0.7f;

        [Header("명중 스파크 (피해 틱과 동기화)")]
        [Range(0, 8)] public int impactSparkCount = 6;
        [Min(0f)] public float impactSparkLength = 0.32f;
        [Min(0.005f)] public float impactSparkWidth = 0.02f;
        [Min(0.01f)] public float impactSparkDuration = 0.09f;

        [Header("시작/명중 효과")]
        [Tooltip("시작점과 실제 명중점에서 반복 재생할 Point 필터 스프라이트입니다.")]
        public Sprite[] startEndFrames = new Sprite[8];
        [Min(0f)] public float visualFadeDuration = 0.08f;

        public float MaxRange => Mathf.Max(0.01f, range);
        public float DamageInterval => Mathf.Max(0.01f, damageInterval);
        public float DamagePerTick => Mathf.Max(0f, damagePerSecond) * DamageInterval;
        public float OuterWidth => Mathf.Max(0.01f, outerWidth);
        public float CoreWidth => Mathf.Clamp(coreWidth, 0.01f, OuterWidth);
        public float AirOuterWidth => Mathf.Clamp(airOuterWidth, 0.01f, OuterWidth);
        public float AirCoreWidth => Mathf.Clamp(airCoreWidth, 0.01f, AirOuterWidth);
        public int ConnectedStrandCount => Mathf.Clamp(connectedStrandCount, 2, 6);
        public int ImpactSparkCount => Mathf.Clamp(impactSparkCount, 0, 8);
        public int VisualSegments => Mathf.Clamp(visualSegments, 4, 24);
        public float PathRefreshInterval => Mathf.Max(0.01f, pathRefreshInterval);
        public bool HasEndpointFrames => startEndFrames != null && startEndFrames.Length > 0 &&
            System.Array.TrueForAll(startEndFrames, frame => frame != null);
    }
}
