using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>저장된 씬/프리팹을 수정하지 않고 실제 Physics2D와 렌더러를 검증합니다.</summary>
    public static class VoidRayWeaponVerification
    {
        private const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/VoidRay.asset";
        private const string BodyTexturePath =
            "Assets/Nytherion/Art/Combat/VFX/Sprites/VoidRayEffect.png";
        private const string Output = "output/voidray";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Tools/Nytherion/Void Ray/Verify Beam %#&v")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[VoidRayVerification] 플레이 모드를 종료한 뒤 실행해 주세요.");
                return;
            }

            var source = AssetDatabase.LoadAssetAtPath<VoidRayWeaponData>(DataPath);
            Require(source != null, "VoidRay 데이터 누락");
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            int failures = 0;
            Check("허공: 가는 다중 번개, 최대 거리, 끝 효과 숨김", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    f.Start();
                    Require(!f.Beam.HasHit && !f.Beam.HasTargetHit, "허공에서 명중 상태");
                    Equal(source.MaxRange, f.Beam.CurrentLength);
                    Equal(source.AirCoreWidth, f.Core.startWidth);
                    Require(f.Core.endColor.a < f.Core.startColor.a,
                        "허공 광선 끝 투명도 그라데이션 누락");
                    Require(f.Core.positionCount == source.AirVisualSegments,
                        "허공 광선 마디 수가 적용되지 않음");
                    Require(source.AirStrandCount == 0 || f.Secondary.enabled,
                        "허공의 가는 보조 광선 누락");
                    Require(source.AirStrandCount == 0 ||
                        f.Secondary.positionCount < f.Core.positionCount,
                        "허공 보조 광선이 주 광선 전체를 평행하게 따라감");
                    Require(!f.Strand(source.AirStrandCount).enabled && !f.ImpactSpark(0).enabled &&
                        !f.EndEffect.enabled, "허공에서 불필요한 연결/명중 효과가 남음");
                    Require(f.Core.sharedMaterial != null &&
                        f.Core.sharedMaterial.shader.name == "Nytherion/Combat/Void Ray Additive",
                        "Void Ray 전용 재질 누락");
                    Require(f.StartEffect.sprite != null &&
                        f.StartEffect.sprite.name.StartsWith("VoidRayStart_"),
                        "총구의 VoidRayStart 효과 누락");
                    Texture bodyTexture = f.Core.sharedMaterial.mainTexture;
                    Require(bodyTexture != null && AssetDatabase.GetAssetPath(bodyTexture) == BodyTexturePath,
                        "VoidRayEffect 몸통 텍스처 누락");
                    Require(bodyTexture.filterMode == FilterMode.Point &&
                        bodyTexture.wrapMode == TextureWrapMode.Repeat,
                        "VoidRayEffect Point/Repeat 임포트 설정 누락");
                    Object[] bodyAssets = AssetDatabase.LoadAllAssetsAtPath(BodyTexturePath);
                    int validBodyFrameCount = 0;
                    for (int i = 0; i < bodyAssets.Length; i++)
                    {
                        if (!(bodyAssets[i] is Sprite bodyFrame)) continue;
                        if (Mathf.Approximately(bodyFrame.rect.width, 32f) &&
                            Mathf.Approximately(bodyFrame.rect.height, 32f))
                            validBodyFrameCount++;
                    }
                    Require(validBodyFrameCount == source.BodyFrameCount,
                        "VoidRayEffect 32x32 프레임 수가 설정과 다름");
                    Require(f.Core.textureMode == LineTextureMode.Tile,
                        "VoidRayEffect 길이 방향 반복 설정 누락");
                    var flowProperties = new MaterialPropertyBlock();
                    f.Core.GetPropertyBlock(flowProperties);
                    float firstFlowOffset = flowProperties.GetFloat(
                        Shader.PropertyToID("_FlowOffset"));
                    float firstBodyFrame = flowProperties.GetFloat(
                        Shader.PropertyToID("_FrameIndex"));
                    Equal(source.BodyFrameCount, Mathf.RoundToInt(flowProperties.GetFloat(
                        Shader.PropertyToID("_FrameCount"))));
                    Vector3 firstTracePosition = f.Secondary.GetPosition(0);
                    Require(f.Core.sharedMaterial.HasProperty("_StrokeExpansion") &&
                        f.Core.sharedMaterial.GetFloat("_StrokeExpansion") >= 1f,
                        "VoidRayEffect 선 굵기 확장 설정 누락");
                    Require(f.StartEffect.sharedMaterial != f.Core.sharedMaterial,
                        "몸통 텍스처가 시작/명중 효과 재질에 적용됨");
                    Vector3 firstVisualEnd = f.Core.GetPosition(f.Core.positionCount - 1);
                    Require(Vector2.Distance(firstVisualEnd, f.Beam.EndPoint) > 0.01f,
                        "허공 광선 끝점이 논리 사거리에 고정됨");
                    f.Capture("air_start");
                    Vector3 before = f.Core.GetPosition(5);
                    f.RenderAt(0.37f);
                    Require(Vector3.Distance(before, f.Core.GetPosition(5)) > 0.001f, "번개 경로가 정지함");
                    Require(Vector3.Distance(firstVisualEnd,
                        f.Core.GetPosition(f.Core.positionCount - 1)) > 0.01f,
                        "허공 광선 끝점이 움직이지 않음");
                    f.Core.GetPropertyBlock(flowProperties);
                    float nextFlowOffset = flowProperties.GetFloat(
                        Shader.PropertyToID("_FlowOffset"));
                    float nextBodyFrame = flowProperties.GetFloat(
                        Shader.PropertyToID("_FrameIndex"));
                    Require(Mathf.Abs(Mathf.DeltaAngle(firstFlowOffset * 360f,
                        nextFlowOffset * 360f)) > 1f,
                        "허공 광선의 텍스처 무늬가 길이 방향으로 흐르지 않음");
                    Require(!Mathf.Approximately(firstBodyFrame, nextBodyFrame),
                        "VoidRayEffect 4프레임 애니메이션이 재생되지 않음");
                    Require(f.Secondary.GetPosition(0).x > firstTracePosition.x + 0.1f,
                        "허공의 짧은 에너지 조각이 광선을 따라 전진하지 않음");
                    f.VerifyEndpoints(false);
                    f.Capture("air");
                }
            });
            Check("적 Trigger: 선명한 연결, 여러 Collider에도 틱당 피해 1회", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    var target = f.Target(new Vector2(3f, 0f), 3);
                    f.Start();
                    Require(f.Beam.HasTargetHit && f.Secondary.enabled, "적 연결 실패");
                    Require(!f.Outer.enabled, "적 연결 중 연하고 굵은 외곽선이 표시됨");
                    Require(!f.EndEffect.enabled, "적 중심에 이미지 기반 명중 효과가 남음");
                    Require(f.Core.positionCount == source.VisualSegments,
                        "적 연결 광선 마디 수가 변경됨");
                    for (int i = 0; i < source.ConnectedStrandCount; i++)
                        Require(f.Strand(i).enabled, "번개 가닥 누락: " + i);
                    Equal(source.CoreWidth, f.Core.startWidth);
                    Equal(f.Core.startColor.a, f.Core.endColor.a);
                    Require(f.Core.sortingOrder >= source.beamSortingOrder,
                        "연결 광선이 몬스터보다 낮은 순서에 표시됨");
                    Equal(3f, f.Beam.CurrentLength);
                    Equal(0f, Vector2.Distance(target.transform.position, f.Beam.EndPoint));
                    f.Tick(0f);
                    Equal(source.DamagePerTick, target.Damage);
                    Require(f.Core.startWidth > source.CoreWidth * 1.2f, "피해 틱 맥동 누락");
                    Require(f.ImpactSpark(0).enabled &&
                        f.ImpactSpark(0).GetPosition(0) != f.ImpactSpark(0).GetPosition(1),
                        "피해 틱 사각형 불똥 누락");
                    Require(f.ImpactSpark(0).sortingOrder >= source.impactSparkSortingOrder,
                        "사각형 불똥 정렬 순서가 몬스터보다 낮음");
                    Require(f.ImpactSpark(0).startWidth >= 0.04f,
                        "사각형 불똥 크기가 픽셀 화면에서 너무 작음");
                    var sparkProperties = new MaterialPropertyBlock();
                    f.ImpactSpark(0).GetPropertyBlock(sparkProperties);
                    Require(sparkProperties.GetTexture(Shader.PropertyToID("_MainTex")) ==
                        Texture2D.whiteTexture, "사각형 불똥이 이미지 텍스처를 사용함");
                    Require(sparkProperties.GetFloat(Shader.PropertyToID("_Intensity")) >= 2f,
                        "사각형 불똥 발광 강도가 부족함");
                    f.Tick(0.05f);
                    Equal(source.DamagePerTick, target.Damage);
                    f.Tick(0.35f);
                    Equal(source.DamagePerTick * 4f, target.Damage);
                    f.VerifyEndpoints();
                    f.Capture("connected");
                }
            });
            Check("적 연쇄: 중심 이펙트와 함께 가까운 다음 적으로 연결", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    var targets = new List<VoidRayVerificationTarget>();
                    for (int i = 0; i <= source.MaxTargetCount; i++)
                    {
                        Vector2 position = i == 0
                            ? new Vector2(1.2f, 0f)
                            : new Vector2(1.2f + i * 0.9f, i % 2 == 1 ? 0.65f : -0.65f);
                        targets.Add(f.Target(position, i == 0 ? 2 : 1));
                    }
                    f.Start(); f.Tick(0f);
                    Require(f.Beam.CurrentTargetCount == source.MaxTargetCount,
                        "동시 타격 대상 수가 설정값과 다름");
                    for (int i = 0; i < source.MaxTargetCount; i++)
                    {
                        Equal(source.DamagePerTick, targets[i].Damage);
                        int sparkIndex = i * source.ImpactSparkCount;
                        LineRenderer spark = f.ImpactSpark(sparkIndex);
                        Require(spark.enabled, "연쇄 대상 중심의 사각형 불똥 누락: " + i);
                        Vector2 sparkCenter = (spark.GetPosition(0) + spark.GetPosition(1)) * 0.5f;
                        float sparkDistance = Vector2.Distance(
                            targets[i].transform.position, sparkCenter);
                        Require(sparkDistance >= source.OuterWidth * 0.5f &&
                            sparkDistance <= source.OuterWidth,
                            "불똥이 광선 안에 묻히거나 중심에서 너무 멀리 생성됨: " + i);
                        int anchorIndex = (i + 1) * (source.VisualSegments - 1);
                        Equal(0f, Vector2.Distance(targets[i].transform.position,
                            f.Core.GetPosition(anchorIndex)));
                    }
                    Equal(0f, targets[source.MaxTargetCount].Damage);
                    Require(f.Core.positionCount ==
                        source.MaxTargetCount * (source.VisualSegments - 1) + 1,
                        "연쇄 구간 수와 광선 마디 수가 다름");
                    Equal(0f, Vector2.Distance(
                        targets[source.MaxTargetCount - 1].transform.position, f.Beam.EndPoint));
                }
            });
            Check("근거리 연결: 구간 길이에 맞춰 두께와 흔들림 축소", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    f.Target(new Vector2(0.35f, 0f));
                    f.Start();
                    Require(f.Beam.HasTargetHit, "근거리 대상을 찾지 못함");
                    Require(f.Core.startWidth < source.CoreWidth * 0.7f,
                        "짧은 광선의 두께가 거리와 무관하게 유지됨");
                    float maxDeviation = 0f;
                    for (int i = 0; i < f.Core.positionCount; i++)
                        maxDeviation = Mathf.Max(maxDeviation,
                            Mathf.Abs(f.Core.GetPosition(i).y - f.Beam.StartPoint.y));
                    Require(maxDeviation <= f.Beam.CurrentLength * 0.3f,
                        "짧은 광선의 흔들림이 구간 길이에 비해 너무 큼");
                }
            });
            Check("벽/장애물: 가는 방전으로 차단, 뒤의 적은 무피해", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    var target = f.Target(new Vector2(4f, 0f));
                    f.Wall(new Vector2(1f, 0f), true);
                    f.Wall(new Vector2(2f, 0f), false);
                    f.Start(); f.Tick(0.3f);
                    Require(f.Beam.HasHit && !f.Beam.HasTargetHit, "벽을 적 연결로 처리함");
                    Equal(source.AirCoreWidth, f.Core.startWidth);
                    Equal(1.8f, f.Beam.CurrentLength);
                    Equal(0f, target.Damage);
                    Require(!f.EndEffect.enabled, "벽 충돌점에 이미지 기반 효과가 남음");
                    Require(!f.ImpactSpark(0).enabled, "피해 없는 벽에서 불똥이 재생됨");
                    f.VerifyEndpoints();
                }
            });
            Check("조준 이탈/재진입: 즉시 가늘어짐, 피해 주기는 유지", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    var target = f.Target(new Vector2(3f, 0f));
                    f.Start(); f.Tick(0f);
                    target.transform.position += Vector3.up;
                    f.Refresh();
                    Require(!f.Beam.HasTargetHit && !f.EndEffect.enabled, "이탈 후 적 연결 유지");
                    Equal(source.AirCoreWidth, f.Core.startWidth);
                    target.transform.position -= Vector3.up;
                    f.Refresh(); f.Tick(0.05f);
                    Require(f.Beam.HasTargetHit, "재진입 연결 실패");
                    Equal(source.DamagePerTick, target.Damage);
                }
            });
            Check("피해 중 비활성화: 참조/연결 즉시 해제", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    var target = f.Target(new Vector2(3f, 0f));
                    target.DisableOnHit = true;
                    f.Start(); f.Tick(0.3f);
                    Equal(source.DamagePerTick, target.Damage);
                    Require(!f.Beam.HasTargetHit && !f.Beam.HasHit, "사라진 대상 연결 유지");
                    Equal(source.AirCoreWidth, f.Core.startWidth);
                }
            });
            Check("마우스 조준 자세: 무기 회전과 좌우 스프라이트 보정", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    Vector2[] directions =
                    {
                        Vector2.right,
                        Vector2.up,
                        Vector2.left,
                        Vector2.down
                    };
                    for (int i = 0; i < directions.Length; i++)
                    {
                        Vector2 target = directions[i] * 3f;
                        f.Aim(target);
                        Vector2 expectedDirection =
                            ((Vector2)f.PlayerCenter + target - (Vector2)f.Owner.transform.position).normalized;
                        Require(Vector2.Dot(f.Owner.transform.right, expectedDirection) > 0.999f,
                            "무기가 마우스 방향을 바라보지 않음: " + directions[i]);

                        float angle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
                        Vector2 expectedOffset = Quaternion.Euler(0f, 0f, angle) *
                            (Vector2)source.visualPositionOffset;
                        Equal(expectedOffset.x, f.Owner.transform.localPosition.x);
                        Equal(expectedOffset.y, f.Owner.transform.localPosition.y);
                    }

                    f.Aim(new Vector2(-3f, 0f));
                    Require(f.Owner.transform.localScale.x > 0f &&
                        f.Owner.transform.localScale.y < 0f,
                        "왼쪽 조준에서 무기 스프라이트 상하 반전이 적용되지 않음");
                }
            });
            Check("회전한 총구에서 마우스 방향으로 출발하는 광선", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    f.Target(new Vector2(0f, 3f));
                    f.Aim(new Vector2(0f, 3f));
                    f.Start();
                    Require(f.Beam.HasTargetHit, "발사 중 조준 변경 실패");
                    Vector2 firstSegment =
                        f.Core.GetPosition(1) - f.Core.GetPosition(0);
                    Require(Vector2.Dot(firstSegment.normalized,
                        f.Owner.transform.right) > 0.9f,
                        "광선이 회전한 총구 방향에서 출발하지 않음");
                    f.VerifyEndpoints();
                    Vector2 end = f.Beam.EndPoint;
                    f.RenderAt(0.73f);
                    Equal(0f, Vector2.Distance(end, f.Beam.EndPoint));
                }
            });
            Check("공격 해제: 잔상은 남아도 즉시 피해 중지", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    var target = f.Target(new Vector2(3f, 0f));
                    f.Start(); f.Tick(0f);
                    f.Beam.StopFiring(); f.Tick(0.4f);
                    Require(!f.Beam.IsFiring && f.Beam.IsFading && !f.Beam.HasTargetHit, "발사 종료 실패");
                    Equal(source.DamagePerTick, target.Damage);
                }
            });
            Check("빠른 재입력: 첫 타격을 반복할 수 없음", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    var target = f.Target(new Vector2(3f, 0f));
                    f.Start(); f.Tick(0f);
                    for (int i = 1; i <= 5; i++)
                    {
                        Invoke(f.Owner, "PrepareDamageSchedule", f.StartTime + i * 0.01d);
                        f.Tick(i * 0.01f);
                    }
                    Equal(source.DamagePerTick, target.Damage);
                }
            });
            Check("15/60/144 FPS: 1초 동안 동일한 누적 피해", results, ref failures, () =>
            {
                foreach (int fps in new[] { 15, 60, 144 })
                using (var f = new Fixture(source))
                {
                    var target = f.Target(new Vector2(3f, 0f));
                    f.Start();
                    for (int frame = 0; frame < fps; frame++) f.Tick(frame / (float)fps);
                    Equal(source.damagePerSecond, target.Damage);
                }
            });
            File.WriteAllLines(Output + "/verification.txt", results);
            string summary = $"[VoidRayVerification] {results.Count - failures}/{results.Count} 통과: {Output}/verification.txt";
            if (failures == 0) Debug.Log(summary);
            else Debug.LogError(summary);
        }

        private static void Check(string name, List<string> results, ref int failures, Action action)
        {
            try { action(); results.Add("PASS " + name); }
            catch (Exception e) { failures++; results.Add("FAIL " + name + ": " + e); }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Equal(float expected, float actual) =>
            Require(Mathf.Abs(expected - actual) < 0.01f, $"예상={expected}, 실제={actual}");
        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Private).Invoke(target, args);

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly VoidRayWeaponData data;
            public readonly VoidRayWeapon Owner;
            public VoidRayBeam Beam;
            public double StartTime;
            public Vector3 PlayerCenter => root.transform.position;
            public LineRenderer Outer => Beam.transform.Find("OuterLine").GetComponent<LineRenderer>();
            public LineRenderer Core => Beam.transform.Find("CoreLine").GetComponent<LineRenderer>();
            public LineRenderer Secondary => Beam.transform.Find("SecondaryLine").GetComponent<LineRenderer>();
            public SpriteRenderer StartEffect => Beam.transform.Find("StartEffect").GetComponent<SpriteRenderer>();
            public SpriteRenderer EndEffect => Beam.transform.Find("EndEffect").GetComponent<SpriteRenderer>();
            public LineRenderer Strand(int index) => Beam.transform
                .Find(index == 0 ? "SecondaryLine" : "ConnectedStrand" + (index + 1))
                .GetComponent<LineRenderer>();
            public LineRenderer ImpactSpark(int index) => Beam.transform
                .Find("ImpactSpark" + (index + 1)).GetComponent<LineRenderer>();

            public Fixture(VoidRayWeaponData source)
            {
                root = new GameObject("[VoidRayVerification]") { hideFlags = HideFlags.HideAndDontSave };
                root.transform.position = new Vector3(20000f, 20000f, 0f);
                data = Object.Instantiate(source);
                data.firePointOffset = Vector3.zero;
                Owner = Object.Instantiate(source.weaponPrefab, root.transform) as VoidRayWeapon;
                Require(Owner != null, "VoidRay 무기 프리팹 오류");
                Owner.transform.localPosition = Vector3.zero;
                Owner.transform.localRotation = Quaternion.identity;
                Owner.transform.localScale = Vector3.one * source.visualScale;
                Owner.Initialize(data);
            }

            public void Start()
            {
                StartTime = Time.timeAsDouble + 1d;
                Invoke(Owner, "PrepareDamageSchedule", StartTime);
                Beam = Object.Instantiate(data.projectilePrefab, root.transform).GetComponent<VoidRayBeam>();
                Physics2D.SyncTransforms();
                Beam.Initialize(Owner, Owner.firePoint, data, null);
            }

            public void Aim(Vector2 localTarget)
            {
                Vector3 mouseWorldPosition = root.transform.position + (Vector3)localTarget;
                Invoke(Owner, "UpdateAimAndPose", mouseWorldPosition, root.transform.position);
            }

            public VoidRayVerificationTarget Target(Vector2 position, int colliders = 1)
            {
                var obj = new GameObject("검증 적");
                obj.transform.SetParent(root.transform, false);
                obj.transform.localPosition = position;
                var target = obj.AddComponent<VoidRayVerificationTarget>();
                for (int i = 0; i < colliders; i++)
                {
                    var hitbox = new GameObject("피격 Trigger");
                    hitbox.transform.SetParent(obj.transform, false);
                    hitbox.layer = LayerMask.NameToLayer("Enemy");
                    var collider = hitbox.AddComponent<BoxCollider2D>();
                    collider.size = Vector2.one * 0.4f;
                    collider.isTrigger = true;
                }
                return target;
            }

            public void Wall(Vector2 position, bool trigger)
            {
                var obj = new GameObject("검증 벽");
                obj.transform.SetParent(root.transform, false);
                obj.transform.localPosition = position;
                obj.layer = LayerMask.NameToLayer("Wall");
                var collider = obj.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one * 0.4f;
                collider.isTrigger = trigger;
            }

            public void Tick(float elapsed)
            {
                typeof(VoidRayBeam).GetField("elapsed", Private).SetValue(Beam, elapsed);
                Physics2D.SyncTransforms();
                Invoke(Beam, "ProcessDamageTicks", StartTime + elapsed);
                Invoke(Beam, "UpdateVisual");
            }
            public void Refresh()
            {
                Physics2D.SyncTransforms();
                Invoke(Beam, "UpdateGeometry");
                Invoke(Beam, "UpdateVisual");
            }
            public void RenderAt(float elapsed)
            {
                typeof(VoidRayBeam).GetField("elapsed", Private).SetValue(Beam, elapsed);
                Invoke(Beam, "UpdateVisual");
            }
            public void VerifyEndpoints(bool visualEndMatchesLogical = true)
            {
                Equal(0f, Vector2.Distance(Owner.firePoint.position, Beam.StartPoint));
                Equal(0f, Vector2.Distance(Core.GetPosition(0), Beam.StartPoint));
                if (visualEndMatchesLogical)
                    Equal(0f, Vector2.Distance(Core.GetPosition(Core.positionCount - 1), Beam.EndPoint));
                if (EndEffect.enabled) Equal(0f, Vector2.Distance(EndEffect.transform.position, Beam.EndPoint));
            }

            public void Capture(string name)
            {
                var cameraObject = new GameObject("검증 카메라") { hideFlags = HideFlags.HideAndDontSave };
                var camera = cameraObject.AddComponent<Camera>();
                var renderTexture = new RenderTexture(640, 360, 24);
                var image = new Texture2D(640, 360, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    // GameScene의 기준 해상도 640x360 / Assets PPU 64와 같은 월드-픽셀 비율입니다.
                    camera.orthographic = true;
                    camera.orthographicSize = 360f / (64f * 2f);
                    camera.transform.position = root.transform.position + new Vector3(2.5f, 0f, -10f);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.035f, 0.05f, 0.09f);
                    camera.targetTexture = renderTexture;
                    camera.Render();
                    RenderTexture.active = renderTexture;
                    image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                    Object.DestroyImmediate(image);
                    Object.DestroyImmediate(renderTexture);
                    Object.DestroyImmediate(cameraObject);
                }
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(data);
                Physics2D.SyncTransforms();
            }
        }
    }

    public sealed class VoidRayVerificationTarget : MonoBehaviour, IDamageable
    {
        public float Damage;
        public bool DisableOnHit;
        public void TakeDamage(float damageAmount, bool isChain = false)
        {
            Damage += damageAmount;
            if (DisableOnHit) gameObject.SetActive(false);
        }
    }
}
