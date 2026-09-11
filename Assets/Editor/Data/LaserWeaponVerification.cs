using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    public static class LaserWeaponVerification
    {
        private const string DATA_PATH = "Assets/Nytherion/Data/ScriptableObjects/Weapons/LaserEmitter.asset";
        private const float INITIAL_HEALTH = 100000f;
        private static readonly Vector3 TestPosition = new Vector3(20000f, 20000f, 0f);
        private static readonly FieldInfo HealthField = typeof(EnemyBase).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BeamField = typeof(LaserWeapon).GetField("activeBeam", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AttackTimeField = typeof(WeaponBase).GetField("lastAttackTime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PoolInstanceField = typeof(ObjectPoolManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MaterialPropertiesField = typeof(WeaponLaserBeam).GetField("materialProperties", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo AdvanceMethod = typeof(WeaponLaserBeam).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RecoilReturnDurationField = typeof(LaserWeapon).GetField("recoilReturnDuration", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo AdvanceRecoilMethod = typeof(LaserWeapon).GetMethod("AdvanceRecoil", BindingFlags.Instance | BindingFlags.NonPublic);

        [Serializable]
        private class CheckResult
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        private class VerificationReport
        {
            public string utc;
            public string unityVersion;
            public string scene;
            public bool playMode;
            public string method = "실제 PlayMode의 프리팹, EnemyBase, Physics2D를 사용하고 Advance(deltaTime)를 동기 호출합니다. 쿨다운 경과는 테스트 무기의 lastAttackTime만 조정합니다.";
            public bool passed;
            public bool cleanupComplete;
            public int total;
            public int failures;
            public List<CheckResult> checks = new List<CheckResult>();
        }

        [MenuItem("Tools/Nytherion/Laser Weapon/Verify In Play Mode")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[LaserWeaponVerification] 플레이 모드에서 실행해 주세요.");
                return;
            }

            VerificationReport report = new VerificationReport
            {
                utc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                scene = SceneManager.GetActiveScene().name,
                playMode = Application.isPlaying
            };
            ObjectPoolManager previousPool = ObjectPoolManager.Instance;
            GameObject poolRoot = null;
            try
            {
                Require(HealthField != null && BeamField != null && AttackTimeField != null &&
                    PoolInstanceField != null && MaterialPropertiesField != null && AdvanceMethod != null &&
                    RecoilReturnDurationField != null && AdvanceRecoilMethod != null,
                    "검증에 필요한 런타임 필드/메서드를 찾을 수 없습니다.");
                LaserWeaponData source = AssetDatabase.LoadAssetAtPath<LaserWeaponData>(DATA_PATH);
                Require(source != null && source.weaponPrefab is LaserWeapon && source.projectilePrefab != null &&
                    source.projectilePrefab.GetComponent<WeaponLaserBeam>() != null, "레이저 무기 생성 메뉴를 먼저 실행해 주세요.");

                // 동기 호출 사이에는 다른 프레임이 실행되지 않는다. 기존 풀의 객체와 큐를 보존한다.
                PoolInstanceField.SetValue(null, null);
                poolRoot = new GameObject("[LaserVerification] 전용 오브젝트 풀");
                ObjectPoolManager fixturePool = poolRoot.AddComponent<ObjectPoolManager>();
                fixturePool.Initialize();

                Check(report, "생성 에셋 기본값 및 연결", () =>
                {
                    Equal(4f, source.damage, "틱 피해");
                    Equal(8f, source.range, "길이");
                    Equal(0.6f, source.cooldown, "쿨다운");
                    Equal(1.2f, source.fireDuration, "발사 시간");
                    Equal(0.2f, source.tickInterval, "틱 간격");
                    Require(source.damageTickCount == 3, "발사당 피해 횟수가 3회가 아닙니다.");
                    Equal(1f, source.visualBeamWidth, "레이저 원본 시각 폭");
                    Equal(1f, source.textureTileLength, "레이저 원본 조각 길이");
                    Equal(0f, source.jitterMagnitude, "레이저 선 진동 크기");
                    Require(source.weaponPrefab.weaponData == source, "무기 프리팹의 데이터 연결이 다릅니다.");
                    Require(source.icon != null && source.weaponSprite != null, "아이콘/무기 이미지가 없습니다.");
                    Require(!source.useStaffRecoil && !source.isArchivable, "반동/아카이브 기본 설정이 다릅니다.");
                    LineRenderer renderer = source.projectilePrefab.GetComponentInChildren<LineRenderer>(true);
                    Require(renderer != null && renderer.sharedMaterial != null &&
                        renderer.sharedMaterial.mainTexture != null && renderer.sharedMaterial.mainTexture.name == "LaserEffect2",
                        "레이저 라인에 LaserEffect2.png 재질이 연결되지 않았습니다.");
                    Equal(10f, renderer.sharedMaterial.GetFloat("_AnimationSpeed"),
                        "LaserEffect2 재생 속도");
                    Transform startEffect = source.projectilePrefab.transform.Find("StartEffect");
                    Transform endEffect = source.projectilePrefab.transform.Find("EndEffect");
                    Require(startEffect != null && endEffect != null &&
                        startEffect.GetComponent<SpriteRenderer>() != null &&
                        endEffect.GetComponent<SpriteRenderer>() != null &&
                        startEffect.GetComponent<Animator>() != null &&
                        endEffect.GetComponent<Animator>() != null,
                        "레이저 시작/종료 애니메이션이 연결되지 않았습니다.");
                });

                Check(report, "장애물이 없으면 카메라 화면 끝까지 표시", () =>
                {
                    Camera camera = Camera.main;
                    Require(camera != null, "Main Camera가 없습니다.");
                    float planeDistance = Vector3.Dot(
                        new Vector3(camera.transform.position.x, camera.transform.position.y, 0f) - camera.transform.position,
                        camera.transform.forward);
                    Require(planeDistance > 0f, "Main Camera가 z=0 게임 평면을 바라보지 않습니다.");
                    Vector3 cameraCenter = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, planeDistance));
                    cameraCenter.z = 0f;
                    using (Fixture fixture = new Fixture(source, cameraCenter))
                    {
                        fixture.Data.obstructionLayers = 0;
                        WeaponLaserBeam beam = fixture.Fire();
                        LineRenderer renderer = beam.GetComponentInChildren<LineRenderer>();
                        Require(renderer != null && renderer.positionCount > 2, "번개 라인 포인트가 없습니다.");
                        for (int i = 0; i < renderer.positionCount; i++)
                        {
                            Vector3 viewport = camera.WorldToViewportPoint(renderer.GetPosition(i));
                            Require(viewport.x >= -0.001f && viewport.x <= 1.001f &&
                                viewport.y >= -0.001f && viewport.y <= 1.001f,
                                $"라인 포인트 {i}가 카메라 밖에 있습니다: {viewport}");
                        }
                        Vector3 endViewport = camera.WorldToViewportPoint(
                            renderer.GetPosition(renderer.positionCount - 1));
                        Require(endViewport.x > 0.99f && endViewport.x <= 1.001f,
                            $"장애물이 없는데 화면 오른쪽 끝까지 도달하지 않았습니다: {endViewport.x}");
                    }
                });

                CheckFixture(report, source, "첫 틱 즉시 적용 및 일시정지", fixture =>
                {
                    EnemyBase enemy = fixture.Enemy(new Vector2(3f, 0f));
                    WeaponLaserBeam beam = fixture.Fire();
                    Equal(4f, Damage(enemy), "첫 틱");
                    Advance(beam, 0f);
                    Equal(4f, Damage(enemy), "정지 중 추가 피해");
                    Require(beam.IsFiring, "정지 중 발사가 종료됐습니다.");
                    Advance(beam, 0.199f);
                    Equal(4f, Damage(enemy), "틱 간격 이전");
                    Advance(beam, 0.001f);
                    Equal(8f, Damage(enemy), "0.2초 두 번째 틱");
                });

                CheckFixture(report, source, "스크립트 재로딩 후 재질 속성 복구", fixture =>
                {
                    WeaponLaserBeam beam = fixture.Fire();
                    MaterialPropertiesField.SetValue(beam, null);
                    Advance(beam, 0.01f);
                    Require(MaterialPropertiesField.GetValue(beam) != null,
                        "MaterialPropertyBlock이 복구되지 않았습니다.");
                });

                CheckFixture(report, source, "3회 공격 종료 후 6프레임 소멸 애니메이션 완주", fixture =>
                {
                    EnemyBase enemy = fixture.Enemy(new Vector2(3f, 0f));
                    WeaponLaserBeam beam = fixture.Fire();
                    LineRenderer renderer = beam.GetComponentInChildren<LineRenderer>();
                    Advance(beam, 0.2f);
                    Advance(beam, 0.2f);
                    Equal(12f, Damage(enemy), "총 3회 공격");
                    Require(!beam.IsFiring, "3회 공격 후에도 공격 중입니다.");
                    Advance(beam, 0.15f);
                    Equal(12f, Damage(enemy), "소멸 중 피해");
                    Require(beam.gameObject.activeSelf && fixture.ActiveBeam == beam,
                        "마지막 두 소멸 프레임 전에 빔이 반환됐습니다.");
                    Equal(fixture.Data.visualBeamWidth, renderer.startWidth,
                        "소멸 프레임 중 레이저 폭");
                    Advance(beam, 0.06f);
                    Require(!beam.gameObject.activeSelf && fixture.ActiveBeam == null,
                        "6프레임 재생 후 풀 반환이 누락됐습니다.");
                });

                CheckFixture(report, source, "피해 틱마다 누적되는 스태프 반동과 종료 후 복귀", fixture =>
                {
                    Vector3 restPosition = fixture.Owner.transform.localPosition;
                    WeaponLaserBeam beam = fixture.Fire();
                    float firstTickDistance = Vector3.Distance(restPosition, fixture.Owner.transform.localPosition);
                    Require(firstTickDistance > 0f, "첫 틱에 스태프가 뒤로 밀리지 않았습니다.");
                    Require(Vector3.Dot(
                        (fixture.Owner.transform.localPosition - restPosition).normalized,
                        Vector3.left) > 0.999f, "스태프가 발사 반대 방향으로 밀리지 않았습니다.");

                    Advance(beam, 0.2f);
                    float secondTickDistance = Vector3.Distance(restPosition, fixture.Owner.transform.localPosition);
                    Require(secondTickDistance > firstTickDistance, "두 번째 틱 반동이 누적되지 않았습니다.");

                    Advance(beam, 0.2f);
                    float thirdTickDistance = Vector3.Distance(restPosition, fixture.Owner.transform.localPosition);
                    Require(thirdTickDistance > secondTickDistance, "세 번째 틱 반동이 누적되지 않았습니다.");
                    Require(!beam.IsFiring, "세 번째 틱 후 공격이 끝나지 않았습니다.");

                    float returnDuration = (float)RecoilReturnDurationField.GetValue(fixture.Owner);
                    AdvanceRecoil(fixture.Owner, returnDuration * 0.5f);
                    float returningDistance = Vector3.Distance(restPosition, fixture.Owner.transform.localPosition);
                    Require(returningDistance > 0f && returningDistance < thirdTickDistance,
                        "공격 종료 후 스태프가 부드럽게 복귀하지 않았습니다.");
                    AdvanceRecoil(fixture.Owner, returnDuration);
                    Equal(0f, Vector3.Distance(restPosition, fixture.Owner.transform.localPosition),
                        "스태프 원위치 복귀");
                });

                CheckFixture(report, source, "낮은 프레임률에서도 틱 누락 없음", fixture =>
                {
                    EnemyBase enemy = fixture.Enemy(new Vector2(3f, 0f));
                    WeaponLaserBeam beam = fixture.Fire();
                    Advance(beam, 0.75f);
                    Equal(12f, Damage(enemy), "낮은 프레임률에서도 정확히 3회");
                    Advance(beam, 0.5f);
                    Equal(12f, Damage(enemy), "종료 뒤 추가 피해 없음");
                });

                CheckFixture(report, source, "자식/다중 콜라이더 중복 피해 방지", fixture =>
                {
                    EnemyBase enemy = fixture.Enemy(new Vector2(3f, 0f), 3);
                    WeaponLaserBeam beam = fixture.Fire();
                    Advance(beam, 1.2f);
                    Equal(12f, Damage(enemy), "콜라이더 3개도 총 3회");
                });

                CheckFixture(report, source, "뒤쪽/길이 밖/폭 밖 제외", fixture =>
                {
                    EnemyBase inside = fixture.Enemy(new Vector2(3f, 0f));
                    EnemyBase behind = fixture.Enemy(new Vector2(-1f, 0f));
                    EnemyBase tooFar = fixture.Enemy(new Vector2(9f, 0f));
                    EnemyBase tooWide = fixture.Enemy(new Vector2(3f, 1f));
                    WeaponLaserBeam beam = fixture.Fire();
                    Advance(beam, 1.2f);
                    Equal(12f, Damage(inside), "범위 안");
                    Equal(0f, Damage(behind), "뒤쪽");
                    Equal(0f, Damage(tooFar), "길이 밖");
                    Equal(0f, Damage(tooWide), "폭 밖");
                });

                CheckFixture(report, source, "벽 뒤 차단 및 시각 길이 축소", fixture =>
                {
                    EnemyBase before = fixture.Enemy(new Vector2(1f, 0f));
                    EnemyBase after = fixture.Enemy(new Vector2(3f, 0f));
                    Collider2D wall = fixture.Wall(new Vector2(2f, 0f), false);
                    WeaponLaserBeam beam = fixture.Fire();
                    Advance(beam, 1.2f);
                    Equal(12f, Damage(before), "벽 앞");
                    Equal(0f, Damage(after), "벽 뒤");
                    float expectedLength = wall.bounds.min.x - fixture.Owner.firePoint.position.x;
                    Equal(expectedLength, beam.CurrentLength, "반동 중 총구에서 벽까지 빔 길이", 0.01f);
                });

                CheckFixture(report, source, "트리거 벽 통과", fixture =>
                {
                    EnemyBase enemy = fixture.Enemy(new Vector2(3f, 0f));
                    fixture.Wall(new Vector2(2f, 0f), true);
                    WeaponLaserBeam beam = fixture.Fire();
                    Advance(beam, 1.2f);
                    Equal(12f, Damage(enemy), "트리거 뒤");
                    Equal(8f, beam.CurrentLength, "최대 길이");
                });

                CheckFixture(report, source, "32개를 넘는 대상 모두 관통", fixture =>
                {
                    List<EnemyBase> enemies = new List<EnemyBase>();
                    for (int i = 0; i < 40; i++) enemies.Add(fixture.Enemy(new Vector2(1f + i * 0.15f, 0f)));
                    WeaponLaserBeam beam = fixture.Fire();
                    Advance(beam, 1.2f);
                    for (int i = 0; i < enemies.Count; i++) Equal(12f, Damage(enemies[i]), $"대상 {i + 1}");
                });

                CheckFixture(report, source, "발사 중 이동과 회전 추적", fixture => VerifyAim(fixture, true));
                CheckFixture(report, source, "방향 고정 상태에서 발사 지점 이동", fixture => VerifyAim(fixture, false));

                CheckFixture(report, source, "플레이어 중심 조준과 발사 지점 분리", fixture =>
                {
                    fixture.Owner.firePoint.position = fixture.Owner.transform.position + Vector3.right * 2f;
                    Vector3 targetBetweenPlayerAndMuzzle = fixture.Owner.transform.position + Vector3.right;
                    Physics2D.SyncTransforms();
                    fixture.Owner.Attack(Vector2.right, targetBetweenPlayerAndMuzzle);
                    WeaponLaserBeam beam = fixture.ActiveBeam;
                    Require(beam != null && beam.IsFiring, "레이저가 발사되지 않았습니다.");
                    Require(Vector2.Dot((Vector2)beam.transform.right, Vector2.right) > 0.999f,
                        "마우스가 플레이어와 총구 사이에 있을 때 플레이어 방향으로 역발사됐습니다.");
                    Equal(0f, Vector3.Distance(beam.transform.position, fixture.Owner.firePoint.position),
                        "레이저 시작 위치");
                });

                CheckFixture(report, source, "시작/충돌 지점 애니메이션 위치와 방향", fixture =>
                {
                    fixture.Wall(new Vector2(2f, 0f), false);
                    WeaponLaserBeam beam = fixture.Fire();
                    Transform startEffect = beam.transform.Find("StartEffect");
                    Transform endEffect = beam.transform.Find("EndEffect");
                    Require(startEffect != null && endEffect != null,
                        "시작/종료 효과 오브젝트가 없습니다.");
                    Equal(0f, Vector3.Distance(startEffect.position, fixture.Owner.firePoint.position),
                        "시작 효과 위치");
                    Equal(0f, Vector3.Distance(endEffect.position,
                        beam.transform.position + beam.transform.right * beam.CurrentLength),
                        "충돌 효과 위치");
                    Require(Vector2.Dot((Vector2)startEffect.right, (Vector2)beam.transform.right) > 0.999f,
                        "시작 효과의 왼쪽이 플레이어 쪽을 향하지 않습니다.");
                    Require(Vector2.Dot((Vector2)endEffect.right, (Vector2)beam.transform.right) < -0.999f,
                        "충돌 효과가 시작 효과와 반대 방향이 아닙니다.");
                });

                CheckFixture(report, source, "무기 비활성화 시 즉시 취소", fixture =>
                {
                    EnemyBase enemy = fixture.Enemy(new Vector2(3f, 0f));
                    WeaponLaserBeam beam = fixture.Fire();
                    fixture.Owner.gameObject.SetActive(false);
                    Require(!beam.IsFiring && !beam.gameObject.activeSelf && fixture.ActiveBeam == null, "비활성화 시 빔이 남았습니다.");
                    Advance(beam, 2f);
                    Equal(4f, Damage(enemy), "취소 이후 피해 없음");
                });

                CheckFixture(report, source, "공격 중 중복 발사 차단 및 종료 후 쿨다운", fixture =>
                {
                    EnemyBase enemy = fixture.Enemy(new Vector2(3f, 0f));
                    WeaponLaserBeam beam = fixture.Fire();
                    fixture.Owner.Attack(Vector2.right);
                    Require(fixture.ActiveBeam == beam && !fixture.Owner.CanAttack(), "공격 중 새 빔이 발사됐습니다.");
                    Equal(4f, Damage(enemy), "중복 공격 없음");
                    Advance(beam, 0.4f);
                    Require(!fixture.Owner.CanAttack(), "발사 종료 직후 쿨다운이 없습니다.");
                    fixture.Owner.Attack(Vector2.right);
                    Equal(12f, Damage(enemy), "쿨다운 중 공격 없음");
                    fixture.ExpireCooldown();
                    Require(fixture.Owner.CanAttack(), "쿨다운 경과 후 공격이 막혔습니다.");
                    fixture.Fire();
                    Equal(16f, Damage(enemy), "재발사 첫 틱");
                });

                CheckFixture(report, source, "동일 풀 객체 재사용 시 틱/외형 초기화", fixture =>
                {
                    EnemyBase enemy = fixture.Enemy(new Vector2(3f, 0f));
                    WeaponLaserBeam first = fixture.Fire();
                    Advance(first, 1.4f);
                    fixture.ExpireCooldown();
                    WeaponLaserBeam second = fixture.Fire();
                    Advance(second, 1.4f);
                    fixture.ExpireCooldown();
                    WeaponLaserBeam reused = fixture.Fire();
                    Require(reused == first, "크기 2인 전용 풀에서 최초 객체가 재사용되지 않았습니다.");
                    Equal(28f, Damage(enemy), "세 번째 발사 첫 틱");
                    LineRenderer renderer = reused.GetComponentInChildren<LineRenderer>();
                    Require(renderer != null && renderer.enabled && renderer.startWidth > 0f && renderer.positionCount > 2,
                        "재사용 시 소멸된 외형이 복원되지 않았습니다.");
                    Advance(reused, 0.2f);
                    Equal(32f, Damage(enemy), "재사용 후 두 번째 틱");
                    Advance(reused, 1f);
                    Equal(36f, Damage(enemy), "3회 발사 각 3틱");
                });
            }
            catch (Exception exception)
            {
                report.checks.Add(new CheckResult { name = "검증 준비/실행", passed = false, detail = Describe(exception) });
            }
            finally
            {
                try
                {
                    if (poolRoot != null) Object.DestroyImmediate(poolRoot);
                }
                finally
                {
                    if (PoolInstanceField != null) PoolInstanceField.SetValue(null, previousPool);
                    Physics2D.SyncTransforms();
                    report.cleanupComplete = ObjectPoolManager.Instance == previousPool && poolRoot == null;
                }
            }

            report.total = report.checks.Count;
            foreach (CheckResult check in report.checks) if (!check.passed) report.failures++;
            report.passed = report.total > 0 && report.failures == 0 && report.cleanupComplete;
            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../output/laser/verification.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            string summary = $"[LaserWeaponVerification] {report.total - report.failures}/{report.total} 통과, 정리={report.cleanupComplete}, 보고서={reportPath}";
            if (report.passed) Debug.Log(summary);
            else Debug.LogError(summary);
        }

        private static void VerifyAim(Fixture fixture, bool followAim)
        {
            fixture.Data.followAim = followAim;
            EnemyBase original = fixture.Enemy(new Vector2(3f, 0f));
            EnemyBase rotated = fixture.Enemy(new Vector2(0f, 8f));
            EnemyBase translated = fixture.Enemy(new Vector2(3f, 5f));
            WeaponLaserBeam beam = fixture.Fire();
            fixture.Owner.transform.position = TestPosition + Vector3.up * 5f;
            fixture.Owner.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Physics2D.SyncTransforms();
            Advance(beam, 0.2f);
            Equal(4f, Damage(original), "이동 전 대상은 첫 틱만");
            Equal(followAim ? 4f : 0f, Damage(rotated), "회전 방향 대상");
            Equal(followAim ? 0f : 4f, Damage(translated), "고정 방향 대상");
            Equal(0f, Vector3.Distance(beam.transform.position, fixture.Owner.firePoint.position), "발사 지점 추적");
        }

        private static void CheckFixture(VerificationReport report, LaserWeaponData source, string name, Action<Fixture> action)
        {
            Check(report, name, () =>
            {
                using (Fixture fixture = new Fixture(source)) action(fixture);
            });
        }

        private static void Check(VerificationReport report, string name, Action action)
        {
            CheckResult result = new CheckResult { name = name };
            try
            {
                action();
                result.passed = true;
                result.detail = "통과";
            }
            catch (Exception exception)
            {
                result.detail = Describe(exception);
            }
            report.checks.Add(result);
        }

        private static string Describe(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
            return exception.GetType().Name + ": " + exception.Message;
        }

        private static void Advance(WeaponLaserBeam beam, float deltaTime)
        {
            AdvanceMethod.Invoke(beam, new object[] { deltaTime });
        }

        private static void AdvanceRecoil(LaserWeapon weapon, float deltaTime)
        {
            AdvanceRecoilMethod.Invoke(weapon, new object[] { deltaTime });
        }

        private static float Damage(EnemyBase enemy)
        {
            return INITIAL_HEALTH - (float)HealthField.GetValue(enemy);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Equal(float expected, float actual, string label, float tolerance = 0.005f)
        {
            Require(Mathf.Abs(expected - actual) <= tolerance, $"{label}: 예상={expected}, 실제={actual}");
        }

        private sealed class Fixture : IDisposable
        {
            public LaserWeaponData Data { get; private set; }
            public LaserWeapon Owner { get; private set; }
            public WeaponLaserBeam ActiveBeam => Owner != null ? (WeaponLaserBeam)BeamField.GetValue(Owner) : null;
            private EnemyData enemyData;
            private GameObject targetsRoot;

            public Fixture(LaserWeaponData source, Vector3? testPosition = null)
            {
                try
                {
                    Data = Object.Instantiate(source);
                    Data.name = "LaserVerificationData";
                    Data.damage = 4f;
                    Data.range = 8f;
                    Data.cooldown = 0.6f;
                    Data.fireDuration = 1.2f;
                    Data.tickInterval = 0.2f;
                    Data.damageTickCount = 3;
                    Data.fadeDuration = 0.12f;
                    Data.beamWidth = 0.55f;
                    Data.followAim = true;
                    Data.firePointOffset = Vector3.zero;
                    Data.targetLayers = LayerMask.GetMask("Enemy");
                    Data.obstructionLayers = LayerMask.GetMask("Wall", "Obstacle");
                    Data.traits = new List<EquipmentTrait>();
                    Data.fireEffectPrefab = null;
                    Data.weaponEffectPrefab = null;
                    Data.useStaffRecoil = false;
                    Vector3 spawnPosition = testPosition ?? TestPosition;
                    Owner = Object.Instantiate(source.weaponPrefab, spawnPosition, Quaternion.identity) as LaserWeapon;
                    Require(Owner != null && Owner.firePoint != null, "무기 프리팹의 발사 지점이 없습니다.");
                    Owner.gameObject.name = "[LaserVerification] 무기";
                    Owner.Initialize(Data);
                    Owner.damageMultiplier = 1f;
                    ExpireCooldown();
                    targetsRoot = new GameObject("[LaserVerification] 대상");
                    targetsRoot.transform.position = spawnPosition;
                    enemyData = ScriptableObject.CreateInstance<EnemyData>();
                    enemyData.enemyName = "LaserVerificationDummy";
                    enemyData.maxHealth = INITIAL_HEALTH;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public EnemyBase Enemy(Vector2 position, int colliderCount = 1)
            {
                GameObject root = new GameObject("레이저 검증 대상");
                root.transform.SetParent(targetsRoot.transform, false);
                root.transform.localPosition = position;
                root.layer = LayerMask.NameToLayer("Enemy");
                EnemyBase enemy = root.AddComponent<EnemyBase>();
                enemy.Initialize(enemyData);
                for (int i = 0; i < colliderCount; i++)
                {
                    GameObject hitbox = new GameObject("Hitbox " + i);
                    hitbox.transform.SetParent(root.transform, false);
                    hitbox.layer = root.layer;
                    BoxCollider2D collider = hitbox.AddComponent<BoxCollider2D>();
                    collider.size = new Vector2(0.15f, 0.15f);
                    collider.isTrigger = true;
                }
                return enemy;
            }

            public Collider2D Wall(Vector2 position, bool trigger)
            {
                GameObject wall = new GameObject("레이저 검증 벽");
                wall.transform.SetParent(targetsRoot.transform, false);
                wall.transform.localPosition = position;
                wall.layer = LayerMask.NameToLayer("Wall");
                BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.4f, 2f);
                collider.isTrigger = trigger;
                return collider;
            }

            public WeaponLaserBeam Fire()
            {
                Physics2D.SyncTransforms();
                Owner.Attack(Vector2.right);
                WeaponLaserBeam beam = ActiveBeam;
                Require(beam != null && beam.IsFiring, "레이저가 발사되지 않았습니다.");
                return beam;
            }

            public void ExpireCooldown()
            {
                AttackTimeField.SetValue(Owner, Time.time - Data.cooldown - 0.1f);
            }

            public void Dispose()
            {
                if (Owner != null) Object.DestroyImmediate(Owner.gameObject);
                if (targetsRoot != null) Object.DestroyImmediate(targetsRoot);
                if (enemyData != null) Object.DestroyImmediate(enemyData);
                if (Data != null) Object.DestroyImmediate(Data);
                Physics2D.SyncTransforms();
            }
        }
    }
}
