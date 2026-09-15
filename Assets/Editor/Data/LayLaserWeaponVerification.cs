using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 프리팹, 적과 Physics2D를 사용하는 수동 플레이 모드 검증입니다.</summary>
    [InitializeOnLoad]
    public static class LayLaserWeaponVerification
    {
        private const string Pending = "LayLaserVerification.Pending";
        private const string Output = "output/laylaser";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo PoolField = typeof(ObjectPoolManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo HealthField = typeof(EnemyBase).GetField("currentHealth", Private);
        private static Report report;
        private static Fixture live;
        private static EnemyBase liveEnemy;
        private static float liveStart;
        private static float previousTimeScale;
        private static bool liveReleased;
        private static double readyAt;

        [Serializable] private class Check { public string name; public bool passed; public string detail; }
        [Serializable] private class Report
        {
            public string unityVersion;
            public string scene;
            public bool playMode;
            public bool passed;
            public List<Check> checks = new List<Check>();
        }

        static LayLaserWeaponVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 2d;
                if (state == PlayModeStateChange.ExitingPlayMode && live != null)
                {
                    live.Dispose();
                    live = null;
                    Time.timeScale = previousTimeScale;
                }
            };
            EditorApplication.update += Update;
        }

        [MenuItem("Tools/Nytherion/Lay Laser/Verify In Play Mode %#&k")]
        public static void Start()
        {
            if (live != null || SessionState.GetBool(Pending, false)) return;
            if (EditorApplication.isPlaying) Run();
            else
            {
                SessionState.SetBool(Pending, true);
                SessionState.SetBool(Pending + ".ExitAfter", true);
                EditorApplication.isPlaying = true;
            }
        }

        private static void Update()
        {
            if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying &&
                !EditorApplication.isPaused && EditorApplication.timeSinceStartup >= readyAt && readyAt > 0d)
            {
                SessionState.SetBool(Pending, false);
                Run();
            }
            if (live == null) return;
            if (!liveReleased && Time.time - liveStart >= 1.25f)
            {
                Record("실제 Update 차징 및 4단계 도달", () => Require(live.Owner.ChargeStage == 4, "4단계 미도달"));
                live.Owner.AttackEnd();
                liveReleased = true;
                liveStart = Time.time;
            }
            else if (liveReleased && Time.time - liveStart >= 1.4f)
            {
                Record("실제 프레임 진행: 2틱, 총 피해 8, 발사 종료", () =>
                {
                    Equal(8f, 1000f - Health(liveEnemy));
                    Require(!live.Owner.IsFiring && !live.Owner.IsCharging, "발사 상태가 남았습니다.");
                });
                live.Dispose();
                live = null;
                Time.timeScale = previousTimeScale;
                SaveReport();
                if (SessionState.GetBool(Pending + ".ExitAfter", false))
                {
                    SessionState.SetBool(Pending + ".ExitAfter", false);
                    EditorApplication.isPlaying = false;
                }
            }
        }

        private static void Run()
        {
            report = new Report { unityVersion = Application.unityVersion, scene = SceneManager.GetActiveScene().name, playMode = Application.isPlaying };
            Directory.CreateDirectory(Output);
            LayLaserWeaponData source = AssetDatabase.LoadAssetAtPath<LayLaserWeaponData>(LayLaserWeaponSetup.DataPath);
            ObjectPoolManager previousPool = ObjectPoolManager.Instance;
            GameObject testPool = null;
            try
            {
                Require(source != null, "데이터 에셋이 없습니다.");
                PoolField.SetValue(null, null);
                testPool = new GameObject("[LayLaserVerification] 전용 풀");
                testPool.AddComponent<ObjectPoolManager>().Initialize();

                Record("데이터베이스, 프리팹, 19/8/8 스프라이트 연결", () =>
                {
                    Require(source.HasValidFrames && source.beamFrames.Length == 8 &&
                        source.startEndFrames.Length == 8, "프레임 연결 누락");
                    Require(source.weaponPrefab is LayLaserWeapon && source.weaponPrefab.weaponData == source, "무기 연결 누락");
                    LayLaserBeam beamPrefab = source.projectilePrefab.GetComponent<LayLaserBeam>();
                    Require(beamPrefab != null, "광선 연결 누락");
                    Require(beamPrefab.transform.Find("StartEffect")?.GetComponent<SpriteRenderer>() != null &&
                        beamPrefab.transform.Find("EndEffect")?.GetComponent<SpriteRenderer>() != null,
                        "광선 시작/끝 렌더러 연결 누락");
                    Require(source.weaponSprite.name == "LayLaser" && source.icon == source.weaponSprite, "외형 연결 오류");
                    Equal(2f, source.GetLength(0)); Equal(4f, source.GetLength(1));
                    Equal(7f, source.GetLength(2)); Equal(10f, source.GetLength(3));
                    Equal(0.25f, source.GetWidth(0)); Equal(0.5f, source.GetWidth(1));
                    Equal(0.85f, source.GetWidth(2)); Equal(1.3f, source.GetWidth(3));
                    ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset");
                    Require(database.allItems.FindAll(item => item == source).Count == 1, "데이터베이스 중복/누락");
                });

                Record("본체 차징 애니메이션, 4단계 경계 및 최대 단계 반복", () =>
                {
                    AnimationClip charge = AssetDatabase.LoadAssetAtPath<AnimationClip>(LayLaserWeaponSetup.ChargeClipPath);
                    AnimationClip fullCharge = AssetDatabase.LoadAssetAtPath<AnimationClip>(LayLaserWeaponSetup.FullChargeClipPath);
                    AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LayLaserWeaponSetup.ControllerPath);
                    Require(charge != null && fullCharge != null && controller != null, "차징 애니메이션 에셋 누락");
                    ObjectReferenceKeyframe[] chargeKeys = SpriteKeys(charge);
                    ObjectReferenceKeyframe[] fullChargeKeys = SpriteKeys(fullCharge);
                    Require(chargeKeys.Length == 12 && fullChargeKeys.Length == 7, "차징 키프레임 수 오류");
                    for (int i = 0; i < LayLaserWeaponData.ChargeStartupFrameCount; i++)
                        Require(chargeKeys[i].value.name == "EnergyCharge_" + i, "차징 프레임 오류: " + i);
                    for (int i = 0; i < LayLaserWeaponData.FullChargeFrameCount; i++)
                        Require(fullChargeKeys[i].value.name == "EnergyCharge_" + (i + 12), "최대 차징 프레임 오류: " + i);
                    Require(AnimationUtility.GetAnimationClipSettings(fullCharge).loopTime,
                        "최대 차징 애니메이션이 반복되지 않습니다.");

                    GameObject weaponPrefab = source.weaponPrefab.gameObject;
                    Animator prefabAnimator = weaponPrefab.GetComponent<Animator>();
                    Require(prefabAnimator != null && prefabAnimator.runtimeAnimatorController == controller,
                        "LayLaser Animator 연결 오류");
                    Require(weaponPrefab.transform.Find("FirePoint/EnergyCharge") == null,
                        "FirePoint에 기존 EnergyCharge 렌더러가 남아 있습니다.");
                    Require(weaponPrefab.GetComponentsInChildren<SpriteRenderer>(true).Length == 1,
                        "LayLaser 본체 외의 중복 SpriteRenderer가 있습니다.");

                    using (Fixture f = new Fixture(source))
                    {
                        f.Owner.Attack(Vector2.right);
                        for (int i = 0; i < 12; i++)
                        {
                            typeof(LayLaserWeapon).GetField("heldTime", Private).SetValue(f.Owner, i * 0.1f + 0.001f);
                            Require(f.Owner.ChargeStage == i / 4 + 1, "차징 단계 오류: " + i);
                        }
                        f.ChargeBy(0.1f);
                        Require(f.Owner.ChargeStage == 4, "최대 차징 단계 오류");
                        f.Owner.Attack(Vector2.left);
                        Require(f.Owner.ChargeStage == 4, "중복 입력이 차징을 초기화했습니다.");
                    }
                });

                for (int stage = 0; stage < 4; stage++)
                {
                    int currentStage = stage;
                    Record($"{stage + 1}단계 길이/폭, 판정, 총구 위치 및 렌더링", () =>
                    {
                        using (Fixture f = new Fixture(source))
                        {
                            float length = source.GetLength(currentStage);
                            float width = source.GetWidth(currentStage);
                            EnemyBase inside = f.Enemy(new Vector2(length - 0.2f, width * 0.5f - 0.1f));
                            EnemyBase far = f.Enemy(new Vector2(length + 0.2f, 0f));
                            EnemyBase wide = f.Enemy(new Vector2(1f, width * 0.5f + 0.2f));
                            f.Fire(currentStage * 0.4f);
                            Equal(length, f.Beam.CurrentLength);
                            Equal(width, f.Beam.CurrentWidth);
                            f.AdvanceBeam(0.21f);
                            Equal(996f, Health(inside)); Equal(1000f, Health(far)); Equal(1000f, Health(wide));
                            SpriteRenderer beamRenderer = f.Beam.transform.Find("Visual").GetComponent<SpriteRenderer>();
                            Transform startEffect = f.Beam.transform.Find("StartEffect");
                            Transform endEffect = f.Beam.transform.Find("EndEffect");
                            Bounds bounds = beamRenderer.bounds;
                            Equal(f.Owner.firePoint.position.x, bounds.min.x);
                            Equal(length, bounds.size.x);
                            Equal(0f, Vector3.Distance(f.Owner.firePoint.position, startEffect.position));
                            Equal(0f, Vector3.Distance(f.Owner.firePoint.position + f.Beam.transform.right * length,
                                endEffect.position));
                            Capture(f, currentStage);
                        }
                    });
                }

                Record("광선과 시작/끝 효과 0~7 프레임 동기 1회 재생", () =>
                {
                    using (Fixture f = new Fixture(source))
                    {
                        f.Fire(0f);
                        SpriteRenderer beamRenderer = f.Beam.transform.Find("Visual").GetComponent<SpriteRenderer>();
                        SpriteRenderer startRenderer = f.Beam.transform.Find("StartEffect").GetComponent<SpriteRenderer>();
                        SpriteRenderer endRenderer = f.Beam.transform.Find("EndEffect").GetComponent<SpriteRenderer>();
                        for (int i = 0; i < 8; i++)
                        {
                            typeof(LayLaserBeam).GetField("elapsed", Private).SetValue(f.Beam, i * 0.1f + 0.001f);
                            Invoke(f.Beam, "UpdateVisual");
                            Require(beamRenderer.sprite.name == "LayLaserEffect_" + i,
                                "광선 프레임 오류: " + i);
                            Require(startRenderer.sprite.name == "LayLaserStartEnd_" + i &&
                                endRenderer.sprite == startRenderer.sprite, "시작/끝 프레임 동기 오류: " + i);
                        }
                    }
                });

                Record("틱 간격, 여러 히트박스 중복 방지, 준비/소멸 무피해", () =>
                {
                    using (Fixture f = new Fixture(source))
                    {
                        EnemyBase enemy = f.Enemy(Vector2.right * 2f, 3);
                        f.Fire(0f);
                        f.AdvanceBeam(0.19f); Equal(1000f, Health(enemy));
                        f.AdvanceBeam(0.02f); Equal(996f, Health(enemy));
                        f.AdvanceBeam(0.18f); Equal(996f, Health(enemy));
                        f.AdvanceBeam(0.02f); Equal(992f, Health(enemy));
                        f.AdvanceBeam(0.38f); Equal(992f, Health(enemy));
                        f.AdvanceBeam(0.02f); Equal(992f, Health(enemy));
                        Require(!f.Owner.IsFiring && !f.Owner.CanAttack(), "발사 종료 쿨다운 오류");
                    }
                });

                Record("낮은 프레임률에서도 틱 2회 유지", () =>
                {
                    using (Fixture f = new Fixture(source))
                    {
                        EnemyBase enemy = f.Enemy(Vector2.right * 2f);
                        f.Fire(0f); f.AdvanceBeam(2f);
                        Equal(992f, Health(enemy)); Require(!f.Owner.IsFiring, "광선이 종료되지 않았습니다.");
                    }
                });

                Record("벽 앞에서 빔 종료 및 트리거 벽 무시", () =>
                {
                    using (Fixture f = new Fixture(source))
                    {
                        f.Wall(Vector2.right, true); f.Wall(Vector2.right * 3f, false);
                        EnemyBase enemy = f.Enemy(Vector2.right * 4f);
                        f.Fire(1.2f); Equal(2.8f, f.Beam.CurrentLength);
                        f.AdvanceBeam(0.4f); Equal(1000f, Health(enemy));
                    }
                });

                Record("좌향/상향 발사와 발사 중 조준 방향 및 총구 추적", () =>
                {
                    foreach (float angle in new[] { 90f, 180f })
                    using (Fixture f = new Fixture(source))
                    {
                        f.Owner.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                        f.Owner.transform.localScale = new Vector3(1f, -1f, 1f);
                        Vector2 direction = angle == 90f ? Vector2.up : Vector2.left;
                        EnemyBase enemy = f.Enemy(direction * 2f);
                        f.Fire(0f); f.AdvanceBeam(0.21f); Equal(996f, Health(enemy));
                        EnemyBase turnedEnemy = f.Enemy(Vector2.right * 2f);
                        f.Owner.transform.rotation = Quaternion.identity;
                        f.AdvanceBeam(0.2f);
                        Equal(996f, Health(turnedEnemy));
                        Equal(0f, Vector2.Distance(Vector2.right, f.Beam.transform.right));
                        Equal(0f, Vector3.Distance(f.Owner.firePoint.position,
                            f.Beam.transform.Find("StartEffect").position));
                        Equal(0f, Vector3.Distance(f.Owner.firePoint.position + Vector3.right * f.Beam.CurrentLength,
                            f.Beam.transform.Find("EndEffect").position));
                        f.Owner.transform.position += Vector3.up;
                        f.AdvanceBeam(0.01f);
                        Equal(0f, Vector3.Distance(f.Owner.firePoint.position, f.Beam.transform.position));
                    }
                });

                Record("차징 시간 100% 감소 시 즉시 4단계", () =>
                {
                    using (Fixture f = new Fixture(source))
                    {
                        f.Data.maxChargeTime = 0f;
                        f.Owner.Attack(Vector2.right);
                        Require(f.Owner.ChargeStage == 4 && f.Owner.ChargePercent == 1f, "즉시 차징 오류");
                        f.Owner.AttackEnd(); Equal(source.GetLength(3), f.Beam.CurrentLength);
                    }
                });

                Record("차징 취소, 발사 중 비활성화, 풀 재사용 초기화", () =>
                {
                    using (Fixture f = new Fixture(source))
                    {
                        f.Owner.Attack(Vector2.right); f.ChargeBy(0.5f);
                        f.Owner.gameObject.SetActive(false);
                        Require(!f.Owner.IsCharging && f.Owner.GetComponent<SpriteRenderer>().sprite == f.Data.weaponSprite,
                            "차징 상태 또는 본체 스프라이트가 남았습니다.");
                        f.Owner.gameObject.SetActive(true); f.Owner.Initialize(f.Data);
                        f.Fire(1.2f);
                        LayLaserBeam old = f.Beam;
                        f.Owner.gameObject.SetActive(false);
                        Require(!old.IsFiring && !old.gameObject.activeSelf, "풀 반환 실패");
                        Require(!old.transform.Find("StartEffect").GetComponent<SpriteRenderer>().enabled &&
                            !old.transform.Find("EndEffect").GetComponent<SpriteRenderer>().enabled,
                            "풀 반환 후 시작/끝 효과가 남았습니다.");
                        f.Owner.gameObject.SetActive(true); f.Owner.Initialize(f.Data); f.Fire(0f);
                        Equal(source.GetLength(0), f.Beam.CurrentLength);
                        Require(f.Beam.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite.name == "LayLaserEffect_0" &&
                            f.Beam.transform.Find("StartEffect").GetComponent<SpriteRenderer>().sprite.name == "LayLaserStartEnd_0",
                            "재사용 프레임 초기화 실패");
                        Require(!f.Owner.CanAttack(), "발사 중 재공격 허용");
                    }
                });
            }
            catch (Exception exception) { report.checks.Add(new Check { name = "검증 실행", detail = exception.ToString() }); }
            finally
            {
                if (testPool != null) Object.DestroyImmediate(testPool);
                PoolField.SetValue(null, previousPool);
            }

            try
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                live = new Fixture(source);
                liveEnemy = live.Enemy(Vector2.right * 2f);
                liveStart = Time.time;
                liveReleased = false;
                live.Owner.Attack(Vector2.right);
            }
            catch (Exception exception)
            {
                report.checks.Add(new Check { name = "실제 프레임 검증 시작", detail = exception.ToString() });
                live?.Dispose(); live = null; Time.timeScale = previousTimeScale; SaveReport();
            }
        }

        private static void Record(string name, Action action)
        {
            Check check = new Check { name = name };
            try { action(); check.passed = true; }
            catch (Exception exception) { check.detail = exception.ToString(); }
            report.checks.Add(check);
        }

        private static void SaveReport()
        {
            report.passed = report.checks.TrueForAll(check => check.passed);
            File.WriteAllText(Output + "/verification.json", JsonUtility.ToJson(report, true));
            Debug.Log($"[LayLaserVerification] {report.checks.FindAll(check => check.passed).Count}/{report.checks.Count} 통과: {Output}/verification.json");
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Equal(float expected, float actual) { Require(Mathf.Abs(expected - actual) < 0.01f, $"예상={expected}, 실제={actual}"); }
        private static float Health(EnemyBase enemy) => (float)HealthField.GetValue(enemy);
        private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);

        private static ObjectReferenceKeyframe[] SpriteKeys(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
                    return AnimationUtility.GetObjectReferenceCurve(clip, binding);
            }
            return Array.Empty<ObjectReferenceKeyframe>();
        }

        private static void Capture(Fixture f, int stage)
        {
            Camera camera = new GameObject("[LayLaserVerification] 렌더 카메라").AddComponent<Camera>();
            RenderTexture target = new RenderTexture(1000, 220, 24);
            Texture2D image = new Texture2D(1000, 220, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.orthographic = true;
                camera.orthographicSize = 1.55f;
                camera.transform.position = f.Owner.transform.position + new Vector3(4.3f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.05f, 0.09f);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, 1000f, 220f), 0, 0);
                image.Apply();
                File.WriteAllBytes($"{Output}/stage-{stage + 1}.png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(camera.gameObject);
            }
        }

        private sealed class Fixture : IDisposable
        {
            public LayLaserWeaponData Data;
            public LayLaserWeapon Owner;
            private GameObject targets;
            private EnemyData enemyData;
            public LayLaserBeam Beam => (LayLaserBeam)typeof(LayLaserWeapon).GetField("activeBeam", Private).GetValue(Owner);

            public Fixture(LayLaserWeaponData source)
            {
                Data = Object.Instantiate(source);
                Data.firePointOffset = Vector3.zero;
                Owner = Object.Instantiate(source.weaponPrefab, new Vector3(20000f, 20000f, 0f), Quaternion.identity) as LayLaserWeapon;
                Owner.Initialize(Data);
                targets = new GameObject("[LayLaserVerification] 대상");
                targets.transform.position = Owner.transform.position;
                enemyData = ScriptableObject.CreateInstance<EnemyData>();
                enemyData.maxHealth = 1000f;
            }

            public void ChargeBy(float time) => Invoke(Owner, "AdvanceCharge", time);
            public void AdvanceBeam(float time) => Invoke(Beam, "Advance", time);
            public void Fire(float charge)
            {
                Physics2D.SyncTransforms();
                Owner.Attack(Vector2.right); ChargeBy(charge); Owner.AttackEnd();
                Require(Beam != null && Beam.IsFiring, "광선 생성 실패");
            }
            public EnemyBase Enemy(Vector2 position, int colliders = 1)
            {
                GameObject root = new GameObject("검증 대상");
                root.transform.SetParent(targets.transform, false); root.transform.localPosition = position;
                EnemyBase enemy = root.AddComponent<EnemyBase>(); enemy.Initialize(enemyData);
                for (int i = 0; i < colliders; i++)
                {
                    GameObject hitbox = new GameObject("Hitbox"); hitbox.transform.SetParent(root.transform, false);
                    hitbox.layer = LayerMask.NameToLayer("Enemy");
                    BoxCollider2D collider = hitbox.AddComponent<BoxCollider2D>(); collider.size = Vector2.one * 0.1f; collider.isTrigger = true;
                }
                Physics2D.SyncTransforms(); return enemy;
            }
            public void Wall(Vector2 position, bool trigger)
            {
                GameObject wall = new GameObject("검증 벽"); wall.transform.SetParent(targets.transform, false);
                wall.transform.localPosition = position; wall.layer = LayerMask.NameToLayer("Wall");
                BoxCollider2D collider = wall.AddComponent<BoxCollider2D>(); collider.size = new Vector2(0.4f, 3f); collider.isTrigger = trigger;
            }
            public void Dispose()
            {
                if (Owner != null) Object.DestroyImmediate(Owner.gameObject);
                if (targets != null) Object.DestroyImmediate(targets);
                if (enemyData != null) Object.DestroyImmediate(enemyData);
                if (Data != null) Object.DestroyImmediate(Data);
                Physics2D.SyncTransforms();
            }
        }
    }
}
