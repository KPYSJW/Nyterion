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
            Check("허공: 가는 단일 번개, 최대 거리, 끝 효과 숨김", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    f.Start();
                    Require(!f.Beam.HasHit && !f.Beam.HasTargetHit, "허공에서 명중 상태");
                    Equal(source.MaxRange, f.Beam.CurrentLength);
                    Equal(source.AirCoreWidth, f.Core.startWidth);
                    Require(!f.Secondary.enabled && !f.Strand(4).enabled && !f.ImpactSpark(0).enabled &&
                        !f.EndEffect.enabled, "허공에서 연결/명중 효과가 남음");
                    Require(f.Core.sharedMaterial != null &&
                        f.Core.sharedMaterial.shader.name == "Nytherion/Combat/Void Ray Additive",
                        "Void Ray 전용 재질 누락");
                    Texture bodyTexture = f.Core.sharedMaterial.mainTexture;
                    Require(bodyTexture != null && AssetDatabase.GetAssetPath(bodyTexture) == BodyTexturePath,
                        "VoidRayEffect 몸통 텍스처 누락");
                    Require(bodyTexture.filterMode == FilterMode.Point &&
                        bodyTexture.wrapMode == TextureWrapMode.Repeat,
                        "VoidRayEffect Point/Repeat 임포트 설정 누락");
                    Require(f.Core.textureMode == LineTextureMode.Tile,
                        "VoidRayEffect 길이 방향 반복 설정 누락");
                    Require(f.Core.sharedMaterial.HasProperty("_StrokeExpansion") &&
                        f.Core.sharedMaterial.GetFloat("_StrokeExpansion") >= 1f,
                        "VoidRayEffect 선 굵기 확장 설정 누락");
                    Require(f.StartEffect.sharedMaterial != f.Core.sharedMaterial,
                        "몸통 텍스처가 시작/명중 효과 재질에 적용됨");
                    Vector3 before = f.Core.GetPosition(5);
                    f.RenderAt(0.37f);
                    Require(Vector3.Distance(before, f.Core.GetPosition(5)) > 0.001f, "번개 경로가 정지함");
                    f.VerifyEndpoints();
                    f.Capture("air");
                }
            });
            Check("적 Trigger: 굵은 연결, 여러 Collider에도 틱당 피해 1회", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    var target = f.Target(new Vector2(3f, 0f), 3);
                    f.Start();
                    Require(f.Beam.HasTargetHit && f.Secondary.enabled && f.EndEffect.enabled, "적 연결 실패");
                    for (int i = 0; i < source.ConnectedStrandCount; i++)
                        Require(f.Strand(i).enabled, "번개 가닥 누락: " + i);
                    Equal(source.CoreWidth, f.Core.startWidth);
                    Equal(2.8f, f.Beam.CurrentLength);
                    f.Tick(0f);
                    Equal(source.DamagePerTick, target.Damage);
                    Require(f.Core.startWidth > source.CoreWidth * 1.2f, "피해 틱 맥동 누락");
                    Require(f.ImpactSpark(0).enabled &&
                        f.ImpactSpark(0).GetPosition(0) != f.ImpactSpark(0).GetPosition(1),
                        "피해 틱 명중 스파크 누락");
                    f.Tick(0.05f);
                    Equal(source.DamagePerTick, target.Damage);
                    f.Tick(0.35f);
                    Equal(source.DamagePerTick * 4f, target.Damage);
                    f.VerifyEndpoints();
                    f.Capture("connected");
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
                    Require(f.Beam.HasHit && !f.Beam.HasTargetHit && !f.Secondary.enabled, "벽을 적 연결로 처리함");
                    Equal(1.8f, f.Beam.CurrentLength);
                    Equal(0f, target.Damage);
                    Require(f.EndEffect.enabled, "실제 벽 충돌점 효과 누락");
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
                    Require(!f.Beam.HasTargetHit && !f.Secondary.enabled && !f.EndEffect.enabled, "이탈 후 연결 유지");
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
                    Require(!f.Beam.HasTargetHit && !f.Beam.HasHit && !f.Secondary.enabled, "사라진 대상 연결 유지");
                }
            });
            Check("방출 중 이동/회전: 총구/끝점 일치 및 흔들림 무피해", results, ref failures, () =>
            {
                using (var f = new Fixture(source))
                {
                    f.Target(new Vector2(0f, 3f));
                    f.Start();
                    f.Owner.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                    f.Owner.transform.localScale = new Vector3(1f, -1f, 1f);
                    f.Refresh();
                    Require(f.Beam.HasTargetHit, "발사 중 조준 변경 실패");
                    f.Owner.transform.position += Vector3.up * 0.2f;
                    f.Refresh(); f.VerifyEndpoints();
                    Equal(2.6f, f.Beam.CurrentLength);
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
            public void VerifyEndpoints()
            {
                Equal(0f, Vector2.Distance(Owner.firePoint.position, Beam.StartPoint));
                Equal(0f, Vector2.Distance(Core.GetPosition(0), Beam.StartPoint));
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
