using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class HomingMagicVerification
    {
        private const string Pending = "Nytherion.HomingMagic.Verify";
        private static readonly Vector2 Origin = new Vector2(10000f, 10000f);
        private static readonly List<string> results = new List<string>();
        private static readonly List<GameObject> temporary = new List<GameObject>();
        private static ObjectPoolManager pool;
        private static ObjectPoolManager previousPool;
        private static readonly FieldInfo PoolInstance = typeof(ObjectPoolManager)
            .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static WeaponData settings;
        private static PlayerData verificationPlayerData;
        private static double readyAt;
        private static bool exitAfter;
        private static bool running;
        private static float previousTimeScale;
        private static bool previousRunInBackground;
        private static bool previousPause;
        private static SimulationMode2D previousSimulationMode;

        static HomingMagicVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 3d;
            };
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (running && EditorApplication.isPaused) EditorApplication.isPaused = false;
            if (File.Exists(HomingMagicSetup.Output + "/stop.request"))
            {
                File.Delete(HomingMagicSetup.Output + "/stop.request");
                SessionState.SetBool(Pending, false);
                EditorApplication.isPlaying = false;
                return;
            }
            if (File.Exists(HomingMagicSetup.Output + "/verify.request"))
            {
                File.Delete(HomingMagicSetup.Output + "/verify.request");
                bool circleOnly = File.Exists(HomingMagicSetup.Output + "/circle-only.request");
                if (circleOnly) File.Delete(HomingMagicSetup.Output + "/circle-only.request");
                SessionState.SetBool(Pending + ".CircleOnly", circleOnly);
                Start();
                exitAfter = true;
                SessionState.SetBool(Pending + ".ExitAfter", true);
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            try
            {
                results.Clear();
                exitAfter = SessionState.GetBool(Pending + ".ExitAfter", false);
                previousTimeScale = Time.timeScale;
                previousRunInBackground = Application.runInBackground;
                previousPause = EditorApplication.isPaused;
                previousSimulationMode = Physics2D.simulationMode;
                Application.runInBackground = true;
                EditorApplication.isPaused = false;
                Time.timeScale = 1f;
                previousPool = ObjectPoolManager.Instance;
                PoolInstance.SetValue(null, null);
                GameObject poolRoot = new GameObject("[HomingMagicVerification] 전용 풀");
                temporary.Add(poolRoot);
                pool = poolRoot.AddComponent<ObjectPoolManager>();
                pool.Initialize();
                settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>(HomingMagicSetup.DataPath));
                running = true;
                pool.StartCoroutine(Execute(Verify()));
            }
            catch (Exception error) { Finish(error); }
        }

        [MenuItem("Tools/Nytherion/Homing Magic/Verify In Play Mode")]
        public static void Start()
        {
            if (running || SessionState.GetBool(Pending, false)) return;
            exitAfter = !EditorApplication.isPlaying;
            SessionState.SetBool(Pending + ".ExitAfter", exitAfter);
            SessionState.SetBool(Pending, true);
            readyAt = EditorApplication.timeSinceStartup + 1d;
            if (exitAfter) EditorApplication.isPlaying = true;
        }

        private static IEnumerator Verify()
        {
            var target = Target(Origin + new Vector2(0f, 7f));
            var playerRoot = new GameObject("검증용 플레이어 데이터");
            playerRoot.transform.position = Origin;
            temporary.Add(playerRoot);
            var player = playerRoot.AddComponent<PlayerManager>();
            var events = playerRoot.AddComponent<EventManager>();
            player.Construct(null, null, events, null);
            verificationPlayerData = ScriptableObject.CreateInstance<PlayerData>();
            player.currentPlayerData = verificationPlayerData;
            GameObject weaponObject = Object.Instantiate(settings.weaponPrefab.gameObject, (Vector3)Origin,
                Quaternion.identity, playerRoot.transform);
            temporary.Add(weaponObject);
            var weapon = weaponObject.GetComponent<RangedWeapon>();
            weapon.Initialize(settings);
            int reportedCount = -1;
            events.OnPlayerRangedAttack += (direction, count, damage, point, tag) => reportedCount = count;
            int nextVariant = 0;
            FieldInfo attackTime = typeof(WeaponBase).GetField("lastAttackTime", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(settings.projectileStartEffectVariants != null && settings.projectileStartEffectVariants.Length == 3,
                "3종 시작 이펙트 연결");
            Physics2D.simulationMode = SimulationMode2D.Script;
            verificationPlayerData.extraProjectiles = 2f;
            for (int attack = 0; attack < 2; attack++)
            {
                target.gameObject.SetActive(attack == 0);
                Physics2D.SyncTransforms();
                weapon.Initialize(settings);
                weapon.Attack(Vector2.up);
                ProjectileStartEffect[] effects = pool.GetComponentsInChildren<ProjectileStartEffect>()
                    .Where(effect => effect.gameObject.activeInHierarchy).ToArray();
                Require(effects.Length == 3 && ActiveBolts().Length == 0, "탄 생성 전에 발사 지점에서 시작 이펙트 3개 재생");
                Vector3[] positions = new Vector3[3];
                float duration = effects.Max(effect => effect.Duration);
                Require(duration > 0f, "시작 애니메이션의 유효한 재생 시간");
                for (int i = 0; i < 3; i++)
                {
                    ProjectileStartEffect effect = effects.Single(value => value.name.Replace("(Clone)", "").Trim() == settings.projectileStartEffectVariants[i].name);
                    positions[i] = effect.transform.position;
                    AnimationClip clip = effect.GetComponent<Animator>().runtimeAnimatorController.animationClips[0];
                    Require(!AnimationUtility.GetAnimationClipSettings(clip).loopTime, "시작 이펙트 단회 재생");
                    Require(AnimationUtility.GetAnimationEvents(clip).Length == 1 &&
                        AnimationUtility.GetAnimationEvents(clip)[0].functionName == nameof(ProjectileStartEffect.OnAnimationComplete),
                        "시작 애니메이션 종료 이벤트 연결");
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                    Require(keys.Length == 5 && keys[0].value.name.Contains("ProjStart" + (i == 0 ? "_" : i.ToString() + "_")), "시작 이미지 대응 및 마지막 프레임 유지");
                }
                if (attack == 0)
                {
                    foreach (ProjectileStartEffect effect in effects) effect.GetComponent<Animator>().speed = 0f;
                    yield return duration + 0.1f;
                    Require(ActiveBolts().Length == 0, "Animator 일시정지 시 시간이 지나도 발사 없음");
                    foreach (ProjectileStartEffect effect in effects) effect.GetComponent<Animator>().speed = 2f;
                }
                // 실제 프레임이 길어져 대기 중 애니메이션이 끝나는 경우를 피합니다.
                foreach (ProjectileStartEffect effect in effects) effect.GetComponent<Animator>().Update(duration * 0.1f);
                Require(ActiveBolts().Length == 0, "시작 이펙트 재생 중 조기 발사 없음");
                yield return (attack == 0 ? duration * 0.3f : duration * 0.8f) + 0.1f;
                HomingProj[] startedBolts = ActiveBolts();
                Require(startedBolts.Length == 3, "시작 이펙트 완료 후 각 지점에서 1발씩 발사");
                Require(effects.All(effect => !effect.gameObject.activeInHierarchy), "시작 이펙트 풀 반환");
                foreach (ProjectileStartEffect effect in effects) effect.GetComponent<Animator>().speed = 1f;
                for (int i = 0; i < 3; i++)
                {
                    HomingProj bolt = startedBolts.Single(value => value.GetComponent<Animator>().runtimeAnimatorController == settings.projectileAnimationVariants[i]);
                    Equal(0f, Vector3.Distance(positions[i], bolt.transform.position), 0.002f);
                    if (attack == 1)
                        Equal(0f, Vector2.Angle((Vector2)(positions[i] - playerRoot.transform.position), bolt.GetComponent<Rigidbody2D>().velocity),
                            0.2f);
                    bolt.GetComponent<CollisionObject>().ReturnToPool();
                }
                yield return duration + 0.05f;
                Require(ActiveBolts().Length == 0, "시작 이펙트의 반복 발사 없음");
            }
            results.Add("PASS 3종 시작 이펙트: Animator 종료 이벤트 발사, Animator 일시정지/속도 변경 반영, 이미지/위치 유지, 단회 발사 및 풀 재사용");
            target.gameObject.SetActive(true);
            Physics2D.SyncTransforms();
            weapon.Initialize(settings);
            weapon.Attack(Vector2.up);
            weapon.gameObject.SetActive(false);
            yield return 0.5f;
            Require(ActiveBolts().Length == 0 && !pool.GetComponentsInChildren<ProjectileStartEffect>().Any(value => value.gameObject.activeInHierarchy),
                "무기 해제 시 대기 발사 취소 및 이펙트 반환");
            weapon.gameObject.SetActive(true);
            results.Add("PASS 시작 이펙트 재생 중 무기 해제: 지연 발사 취소");
            Physics2D.simulationMode = previousSimulationMode;
            // 기존 위치/유도 검증은 시작 연출 검증과 분리해 즉시 발사로 확인합니다.
            settings.projectileStartEffectVariants = null;
            weapon.Initialize(settings);
            verificationPlayerData.extraProjectiles = 2f;
            weapon.Attack(Vector2.left);
            HomingProj[] outwardHomingBolts = ActiveBolts();
            Require(outwardHomingBolts.Length == 3 && settings.homingLaunchDuration > 0f, "바깥 방향 출발 및 유도 대기 시간 설정");
            bool changedHeading = false;
            foreach (HomingProj bolt in outwardHomingBolts)
            {
                Rigidbody2D outwardBody = bolt.GetComponent<Rigidbody2D>();
                Vector2 outward = bolt.transform.position - playerRoot.transform.position;
                Equal(0f, Vector2.Angle(outward, outwardBody.velocity), 0.2f);
                int straightTicks = Mathf.CeilToInt(settings.homingLaunchDuration / Time.fixedDeltaTime);
                for (int tick = 0; tick < straightTicks; tick++)
                {
                    Tick(bolt);
                    Equal(0f, Vector2.Angle(outward, outwardBody.velocity), 0.2f);
                    outwardBody.position += outwardBody.velocity * Time.fixedDeltaTime;
                }
                Vector2 desired = (Vector2)target.GetComponent<Collider2D>().bounds.center - outwardBody.position;
                float beforeHoming = Vector2.Angle(outwardBody.velocity, desired);
                // 소수점 오차로 남은 대기 시간이 있더라도 다음 두 틱 안에는 유도를 시작합니다.
                Tick(bolt);
                Tick(bolt);
                float afterHoming = Vector2.Angle(outwardBody.velocity, desired);
                Require(afterHoming <= beforeHoming + 0.2f, "직진 대기 후 선택한 적 방향으로 회전");
                changedHeading |= afterHoming < beforeHoming - 0.2f;
                bolt.GetComponent<CollisionObject>().ReturnToPool();
            }
            Require(changedHeading, "바깥 방향 직진 이후 실제 적 추적 시작");
            results.Add("PASS 적이 있는 원형 발사: 중심→발사 지점 방향으로 초기 직진, 유도 대기 시간 후 선택한 적 추적");
            weapon.Initialize(settings);
            Require(settings.usePlayerCircleSpawn, "정령의 인도 원형 발사 설정");
            var selectedPoints = new HashSet<int>();
            foreach (int circleCount in new[] { 1, 2, 12, 17 })
            {
                verificationPlayerData.extraProjectiles = circleCount - 1;
                int pointCount = Mathf.Max(circleCount, settings.playerCircleSpawnPointCount);
                for (int attack = 0; attack < 4; attack++)
                {
                    // 이동 및 무기 회전 뒤에도 원의 중심은 플레이어를 따라야 합니다.
                    playerRoot.transform.position = (Vector3)Origin + Vector3.right * (attack * 0.1f);
                    weapon.transform.rotation = Quaternion.Euler(0f, 0f, attack * 70f);
                    attackTime.SetValue(weapon, -settings.cooldown);
                    weapon.Attack(Vector2.up);
                    HomingProj[] circleBolts = ActiveBolts();
                    Require(circleBolts.Length == circleCount && reportedCount == circleCount, "원형 발사 수와 이벤트 수 일치");
                    selectedPoints.Clear();
                    foreach (HomingProj bolt in circleBolts)
                    {
                        Vector2 offset = bolt.transform.position - playerRoot.transform.position;
                        Equal(settings.playerCircleSpawnRadius, offset.magnitude, 0.002f);
                        float index = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg, 360f) * pointCount / 360f;
                        int point = Mathf.RoundToInt(index) % pointCount;
                        Equal(0f, Mathf.Abs(Mathf.DeltaAngle(index * 360f / pointCount, point * 360f / pointCount)), 0.2f);
                        Require(selectedPoints.Add(point), "동일 공격에서 발사 지점 중복 없음");
                        Require(Field<Collider2D>(bolt, "targetCollider") == target.GetComponent<Collider2D>(), "원형 발사 목표 유지");
                        bolt.GetComponent<CollisionObject>().ReturnToPool();
                    }
                }
                results.Add($"PASS 원형 발사 {circleCount}발 × 4회: 반지름, 균등 지점, 중복 방지, 이동/회전, 유도 목표, 공격 이벤트");
            }
            target.gameObject.SetActive(false);
            Physics2D.SyncTransforms();
            foreach (int circleCount in new[] { 1, 2, 17 })
            {
                verificationPlayerData.extraProjectiles = circleCount - 1;
                for (int attack = 0; attack < 8; attack++)
                {
                    attackTime.SetValue(weapon, -settings.cooldown);
                    weapon.Attack(attack % 2 == 0 ? Vector2.left : Vector2.right);
                    HomingProj[] circleBolts = ActiveBolts();
                    Require(circleBolts.Length == circleCount && reportedCount == circleCount, "목표 없는 원형 발사 수");
                    foreach (HomingProj bolt in circleBolts)
                    {
                        Vector2 outward = bolt.transform.position - playerRoot.transform.position;
                        Vector2 velocity = bolt.GetComponent<Rigidbody2D>().velocity;
                        float outwardAngle = Vector2.SignedAngle(outward, velocity);
                        Require(Mathf.Abs(outwardAngle) <= 0.2f && Vector2.Dot(outward, velocity) > 0f,
                            "목표가 없을 때 플레이어 중심에서 발사 지점으로 향하는 방향");
                        Require(Field<Collider2D>(bolt, "targetCollider") == null, "목표 없는 탄의 유도 대상 없음");
                        Tick(bolt);
                        Equal(0f, Vector2.Angle(outward, bolt.GetComponent<Rigidbody2D>().velocity), 0.2f);
                        bolt.GetComponent<CollisionObject>().ReturnToPool();
                    }
                }
                results.Add($"PASS 목표 없는 원형 발사 {circleCount}발 × 8회: 플레이어 중심→발사 지점 벡터, 조준 방향 독립 및 직진 유지");
            }
            target.gameObject.SetActive(true);
            Physics2D.SyncTransforms();
            if (SessionState.GetBool(Pending + ".CircleOnly", false))
            {
                target.gameObject.SetActive(false);
                Physics2D.simulationMode = SimulationMode2D.Script;
                foreach (float distance in new[] { 0.5f, 2f, 7f })
                {
                    EnemyBase speedTarget = Target(Origin + Vector2.right * distance);
                    foreach (Vector2 launchDirection in new[] { Vector2.left, Vector2.up, Vector2.right })
                    {
                        GameObject speedBolt = weapon.SpawnProj(launchDirection, homingTarget: speedTarget.GetComponent<Collider2D>(),
                            targetSelected: true, spawnPosition: Origin);
                        HomingProj movement = speedBolt.GetComponent<HomingProj>();
                        Rigidbody2D speedBody = speedBolt.GetComponent<Rigidbody2D>();
                        Equal(0f, Vector2.Distance(Origin, speedBody.position), 0.002f);
                        Equal(settings.projectileSpeed, speedBody.velocity.magnitude, 0.002f);
                        for (int tick = 0; tick < 20; tick++)
                        {
                            Tick(movement);
                            Equal(settings.projectileSpeed, speedBody.velocity.magnitude, 0.002f);
                        }
                        speedBolt.GetComponent<CollisionObject>().ReturnToPool();
                    }
                    speedTarget.gameObject.SetActive(false);
                }
                results.Add("PASS 발사/유도 속도: 근거리·원거리, 앞/옆/뒤 방향 및 풀 재사용 모두 설정 속도 유지, 물리 위치 초기화");
                foreach (RigidbodyType2D bodyType in new[] { RigidbodyType2D.Kinematic, RigidbodyType2D.Static })
                {
                    EnemyBase victimBody = Target(Origin + Vector2.right * 1.2f);
                    victimBody.GetComponent<Collider2D>().enabled = false;
                    victimBody.gameObject.AddComponent<Rigidbody2D>().bodyType = bodyType;
                    GameObject hurtbox = new GameObject("태그 없는 적 몸체");
                    hurtbox.transform.SetParent(victimBody.transform, false);
                    hurtbox.layer = LayerMask.NameToLayer("Enemy");
                    hurtbox.AddComponent<CircleCollider2D>().radius = 0.12f;
                    GameObject sensor = new GameObject("태그 없는 적 감지 영역");
                    sensor.transform.SetParent(victimBody.transform, false);
                    sensor.layer = LayerMask.NameToLayer("Enemy");
                    CircleCollider2D sensorCollider = sensor.AddComponent<CircleCollider2D>();
                    sensorCollider.isTrigger = true;
                    sensorCollider.radius = 2f;
                    Require(!CollisionObject.IsEnemyCollider(sensorCollider), "감지용 자식 트리거는 피격 영역에서 제외");
                    Require(CollisionObject.IsEnemyCollider(hurtbox.GetComponent<Collider2D>()), "태그 없는 적 자식 몸체 인식");
                    Physics2D.SyncTransforms();
                    GameObject hitBolt = weapon.SpawnProj(Vector2.right, targetSelected: true, spawnPosition: Origin);
                    for (int tick = 0; tick < 30 && hitBolt.activeInHierarchy; tick++)
                    {
                        Tick(hitBolt.GetComponent<HomingProj>());
                        Physics2D.Simulate(Time.fixedDeltaTime);
                    }
                    Require(!hitBolt.activeInHierarchy, bodyType + " 적 자식 몸체 충돌 후 반환");
                    Equal(settings.damage, 10000f - Field<float>(victimBody, "currentHealth"), 0.01f);
                    results.Add("PASS 실제 Physics2D " + bodyType + " 적 자식 몸체: 감지 영역 무시, 피해 1회 및 풀 반환");
                    victimBody.gameObject.SetActive(false);
                }
                foreach (float hitDistance in new[] { 0.6f, 7f })
                {
                    EnemyBase trackingVictim = Target(Origin + Vector2.right * hitDistance);
                    CircleCollider2D victimCollider = trackingVictim.GetComponent<CircleCollider2D>();
                    victimCollider.radius = 0.12f;
                    trackingVictim.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                    Physics2D.SyncTransforms();
                    var trackingBolts = new List<GameObject>();
                    for (int point = 0; point < 12; point++)
                    {
                        float angle = point * Mathf.PI / 6f;
                        Vector2 outwardDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        trackingBolts.Add(weapon.SpawnProj(outwardDirection, homingTarget: victimCollider, targetSelected: true,
                            spawnPosition: Origin + outwardDirection * settings.playerCircleSpawnRadius));
                    }
                    for (int tick = 0; tick < 200 && trackingBolts.Any(value => value.activeInHierarchy); tick++)
                    {
                        foreach (GameObject bolt in trackingBolts)
                        {
                            if (!bolt.activeInHierarchy) continue;
                            Tick(bolt.GetComponent<HomingProj>());
                            Equal(settings.projectileSpeed, bolt.GetComponent<Rigidbody2D>().velocity.magnitude, 0.002f);
                        }
                        Physics2D.Simulate(Time.fixedDeltaTime);
                    }
                    Require(trackingBolts.All(value => !value.activeInHierarchy), "일정 속도 유도탄 12방향 실제 적중 및 반환");
                    Equal(settings.damage * 12f, 10000f - Field<float>(trackingVictim, "currentHealth"), 0.01f);
                    results.Add($"PASS 실제 Physics2D 거리 {hitDistance}: 원의 12방향 출발 후 일정 속도로 적중, 피해 12회 및 전부 반환");
                    trackingVictim.gameObject.SetActive(false);
                }
                TrainingDummy actualDummy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Debug/TrainingDummy.prefab"), (Vector3)Origin + Vector3.right * 1.5f, Quaternion.identity).GetComponent<TrainingDummy>();
                temporary.Add(actualDummy.gameObject);
                actualDummy.Construct(events, null, player);
                actualDummy.Initialize(actualDummy.enemyData);
                int dummyHitCount = 0;
                events.OnEnemyDamagedByPlayerDetailed += hit => { if (hit.Target == actualDummy) dummyHitCount++; };
                Physics2D.SyncTransforms();
                Collider2D dummyCollider = actualDummy.GetComponent<Collider2D>();
                GameObject normalDummyBolt = weapon.SpawnProj(Vector2.right, homingTarget: dummyCollider, targetSelected: true, spawnPosition: Origin);
                for (int tick = 0; tick < 60 && normalDummyBolt.activeInHierarchy; tick++)
                {
                    Tick(normalDummyBolt.GetComponent<HomingProj>());
                    Physics2D.Simulate(Time.fixedDeltaTime);
                }
                Require(!normalDummyBolt.activeInHierarchy && dummyHitCount == 1, "실제 허수아비에 기본 탄 피해 1회 후 즉시 반환");
                results.Add("PASS 실제 TrainingDummy 프리팹: 기본 유도탄 피해 1회 및 풀 반환");
                GameObject piercingDummyBolt = weapon.SpawnProj(Vector2.right, homingTarget: dummyCollider, targetSelected: true, spawnPosition: Origin);
                piercingDummyBolt.GetComponent<PiercingModifier>().enabled = true;
                for (int tick = 0; tick < 100; tick++)
                {
                    Tick(piercingDummyBolt.GetComponent<HomingProj>());
                    Physics2D.Simulate(Time.fixedDeltaTime);
                }
                Require(piercingDummyBolt.activeInHierarchy && dummyHitCount == 2,
                    "허수아비 관통 시 추가 피해 1회, 같은 대상 반복 타격 없음");
                Require(Field<Collider2D>(piercingDummyBolt.GetComponent<HomingProj>(), "targetCollider") == null &&
                    Vector2.Distance(piercingDummyBolt.transform.position, actualDummy.transform.position) > 2f,
                    "허수아비 관통 후 추적 해제 및 이탈, 대상 위치에서 진동 없음");
                piercingDummyBolt.GetComponent<CollisionObject>().ReturnToPool();
                actualDummy.gameObject.SetActive(false);
                results.Add("PASS 실제 TrainingDummy 관통: 같은 대상 재추적/반복 피해 없이 이탈");
                GameObject wallParent = new GameObject("부모에만 태그가 있는 벽");
                temporary.Add(wallParent);
                wallParent.tag = "Wall";
                wallParent.transform.position = Origin + Vector2.right * 1.2f;
                GameObject wallChild = new GameObject("태그 없는 벽 충돌체");
                wallChild.transform.SetParent(wallParent.transform, false);
                wallChild.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
                Physics2D.SyncTransforms();
                GameObject wallBolt = weapon.SpawnProj(Vector2.right, targetSelected: true, spawnPosition: Origin);
                for (int tick = 0; tick < 30 && wallBolt.activeInHierarchy; tick++)
                {
                    Tick(wallBolt.GetComponent<HomingProj>());
                    Physics2D.Simulate(Time.fixedDeltaTime);
                }
                Require(!wallBolt.activeInHierarchy, "태그 없는 자식 벽 충돌 후 반환");
                results.Add("PASS 실제 Physics2D 자식 벽 충돌: 부모 Wall 태그 인식 및 풀 반환");
                Physics2D.simulationMode = previousSimulationMode;
                SessionState.SetBool(Pending + ".CircleOnly", false);
                yield break;
            }
            playerRoot.transform.position = Origin;
            weapon.transform.rotation = Quaternion.identity;
            // 기존 총구 발사 각도·유도·풀 재사용 검증도 별도로 유지합니다.
            settings.usePlayerCircleSpawn = false;
            weapon.Initialize(settings);
            Require(settings.projectileAnimationVariants != null && settings.projectileAnimationVariants.Length == 3,
                "3종 투사체 애니메이션 연결");
            // 공격 사이와 발사 수 변경 시 순서를 유지하고 같은 풀 객체를 반복 재사용합니다.
            foreach (int sequenceCount in new[] { 1, 2, 3, 4, 2, 1 })
            {
                verificationPlayerData.extraProjectiles = sequenceCount - 1;
                for (int attack = 0; attack < 4; attack++)
                {
                    attackTime.SetValue(weapon, -settings.cooldown);
                    weapon.Attack(Vector2.up);
                    Vector2 aim = ((Vector2)target.transform.position - (Vector2)weapon.firePoint.position).normalized;
                    HomingProj[] sequence = ActiveBolts().OrderByDescending(bolt =>
                        Vector2.SignedAngle(aim, bolt.GetComponent<Rigidbody2D>().velocity)).ToArray();
                    Require(sequence.Length == sequenceCount, "순환 검증 발사 수 " + sequenceCount);
                    foreach (HomingProj bolt in sequence)
                    {
                        Animator animation = bolt.GetComponent<Animator>();
                        Require(animation.runtimeAnimatorController == settings.projectileAnimationVariants[nextVariant],
                            $"{sequenceCount}발 묶음 {attack + 1}번째 공격: 이미지 {nextVariant + 1}");
                        Sprite[] frames = animation.runtimeAnimatorController.animationClips
                            .SelectMany(clip => AnimationUtility.GetObjectReferenceCurve(clip,
                                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite")))
                            .Select(key => key.value as Sprite).ToArray();
                        Require(frames.Length == 4 && frames.All(frame => frame != null), "유효한 4프레임 참조");
                        Require(frames.Contains(bolt.GetComponent<SpriteRenderer>().sprite), "첫 프레임 적용");
                        nextVariant = (nextVariant + 1) % 3;
                    }
                    yield return new WaitForSeconds(0.1f);
                    foreach (HomingProj bolt in sequence)
                    {
                        AnimationClip clip = bolt.GetComponent<Animator>().runtimeAnimatorController.animationClips.First();
                        var frames = AnimationUtility.GetObjectReferenceCurve(clip,
                            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                        Require(frames.Any(frame => frame.value == bolt.GetComponent<SpriteRenderer>().sprite),
                            "재사용 후 선택한 이미지의 애니메이션 유지");
                        bolt.GetComponent<CollisionObject>().ReturnToPool();
                    }
                }
                results.Add($"PASS {sequenceCount}발 × 4회 공격: 1→2→3 연속 순환, 풀 재사용 및 애니메이션 유지");
            }
            foreach (float extra in new[] { 0f, 1f, 2f, 4f, 8f, 1.9f, -1f })
            {
                verificationPlayerData.extraProjectiles = extra;
                weapon.Initialize(settings);
                reportedCount = -1;
                weapon.Attack(Vector2.left);
                HomingProj[] countBolts = ActiveBolts();
                int expectedCount = 1 + Mathf.Max(0, Mathf.FloorToInt(extra));
                Require(countBolts.Length == expectedCount && reportedCount == expectedCount,
                    $"증가 효과 {extra}: 실제 발사 수/공격 이벤트 수 {expectedCount}");
                Vector2 countBasis = ((Vector2)target.transform.position - (Vector2)weapon.firePoint.position).normalized;
                float[] countAngles = countBolts.Select(value => Vector2.SignedAngle(countBasis,
                    value.GetComponent<Rigidbody2D>().velocity)).OrderBy(value => value).ToArray();
                float halfSpan = Mathf.Min(30f, (expectedCount - 1) * 7.5f);
                for (int i = 0; i < expectedCount; i++)
                    Equal(expectedCount == 1 ? 0f : Mathf.Lerp(-halfSpan, halfSpan, i / (float)(expectedCount - 1)),
                        countAngles[i], 0.1f);
                foreach (HomingProj bolt in countBolts) bolt.GetComponent<CollisionObject>().ReturnToPool();
                results.Add($"PASS 투사체 증가 {extra}: {expectedCount}발, 중앙 대칭 배치, 공격 이벤트 수 일치");
            }

            // 기존 다중 탄 이동 검증은 투사체 증가 +4를 실제 플레이어 데이터에 적용해 실행합니다.
            verificationPlayerData.extraProjectiles = 4f;
            weapon.Initialize(settings);
            weapon.Attack(Vector2.left);
            HomingProj[] bolts = pool.GetComponentsInChildren<HomingProj>()
                .Where(value => value.gameObject.activeInHierarchy && Vector2.Distance(value.transform.position, Origin) < 2f)
                .OrderByDescending(value => Vector2.SignedAngle(Vector2.up, value.GetComponent<Rigidbody2D>().velocity)).ToArray();
            Require(bolts.Length == 5, "기본 1 + 증가 4 = 5발 생성");
            Vector2 basis = ((Vector2)target.transform.position - (Vector2)weapon.firePoint.position).normalized;
            var launchDirections = bolts.Select(value => value.GetComponent<Rigidbody2D>().velocity.normalized).ToArray();
            var actualAngles = launchDirections.Select(value => Vector2.SignedAngle(basis, value)).OrderBy(value => value).ToArray();
            float[] expectedAngles = settings.homingLaunchAngles.OrderBy(value => value).ToArray();
            for (int i = 0; i < 5; i++) Equal(expectedAngles[i], actualAngles[i], 0.1f);
            results.Add("PASS 증가 +4: 목표 기준 +30/+15/0/-15/-30도, 조준 방향과 무관하게 5발 생성");
            yield return 0.09f;
            foreach (HomingProj bolt in bolts)
                Require(((Vector2)bolt.transform.position - (Vector2)weapon.firePoint.position).magnitude > 0.2f, "실제 Rigidbody2D 이동");
            for (int i = 0; i < bolts.Length; i++)
                Equal(0f, Vector2.Angle(launchDirections[i], bolts[i].GetComponent<Rigidbody2D>().velocity), 0.1f);
            results.Add("PASS 초기 퍼짐 구간에서 각 탄의 발사 방향 유지 및 실제 물리 이동");
            yield return 0.2f;
            int frameCount = 0;
            var renderedFrames = new HashSet<string>();
            for (int i = 0; i < 12; i++)
            {
                foreach (HomingProj bolt in bolts)
                {
                    Sprite sprite = bolt.GetComponent<SpriteRenderer>().sprite;
                    Require(sprite != null && sprite.name.StartsWith("Homing_"), "Homing 스프라이트 연결");
                    renderedFrames.Add(sprite.name);
                }
                frameCount++;
                yield return 0.025f;
            }
            Require(renderedFrames.Count == 4 && frameCount == 12, "실제 4프레임 루프 재생");
            results.Add("PASS Homing.png 32×32 4프레임 실제 Animator 루프 재생");
            var moving = bolts[0];
            Rigidbody2D body = moving.GetComponent<Rigidbody2D>();
            target.transform.position = (Vector3)Origin + new Vector3(5f, 7f);
            Physics2D.SyncTransforms();
            Vector2 before = body.velocity.normalized;
            yield return 0.15f;
            float turn = Vector2.Angle(before, body.velocity.normalized);
            Require(turn > 1f && turn <= settings.homingTurnSpeed * 0.18f, "움직인 목표로 제한 속도 회전");
            Equal(settings.projectileSpeed, body.velocity.magnitude, 0.01f);
            results.Add("PASS 발사 이후 이동한 목표를 부드럽게 추적, 설정된 속도 유지");
            // 근거리에서도 최대 회전 속도를 넘지 않는지 한 물리 틱을 직접 검증합니다.
            moving.Initialize(true, 6f, Vector2.left, target.GetComponent<Collider2D>(), 90f, 0f, 37f);
            target.transform.position = body.position + Vector2.right * 0.2f;
            Physics2D.SyncTransforms();
            Tick(moving);
            float limitedTurn = Vector2.Angle(Vector2.left, body.velocity);
            Require(limitedTurn > 0f && limitedTurn <= 90f * Time.fixedDeltaTime + 0.01f, "근거리 회전 제한");
            Equal(37f, Mathf.DeltaAngle(Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg, body.rotation), 0.01f);
            results.Add("PASS 근거리에서도 초당 회전 제한 유지, 스프라이트 회전 보정과 이동 방향 분리");
            target.gameObject.SetActive(false);
            Tick(moving);
            Vector2 lostDirection = body.velocity.normalized;
            Target(body.position + Vector2.down * 2f);
            for (int i = 0; i < 4; i++) Tick(moving);
            Equal(0f, Vector2.Angle(lostDirection, body.velocity), 0.01f);
            Require(Field<Collider2D>(moving, "targetCollider") == null, "소멸 목표 참조 해제");
            results.Add("PASS 목표 소멸 후 마지막 방향 유지, 주변 새 적으로 재탐색하지 않음");
            foreach (HomingProj bolt in bolts) bolt.GetComponent<CollisionObject>().ReturnToPool();
            foreach (var item in temporary.Where(value => value != null && value.name == "검증 적")) item.SetActive(false);

            // 실제 풀에서 반환/재발사하고 목표 없음, 직선, 수명, 적중 상태 초기화를 확인합니다.
            settings.hasHomingProjectiles = false;
            settings.homingLifetime = 0.25f;
            settings.projectileRotationOffset = 37f;
            for (int i = 0; i < 5; i++)
                weapon.SpawnProj(Vector2.down).GetComponent<CollisionObject>().ReturnToPool();
            GameObject reused = weapon.SpawnProj(Vector2.down);
            var reusedMovement = reused.GetComponent<HomingProj>();
            Require(bolts.Any(bolt => bolt.gameObject == reused), "실제 풀 인스턴스 재사용");
            Require(Field<Collider2D>(reusedMovement, "targetCollider") == null, "이전 목표 초기화");
            Require(!Field<bool>(reused.GetComponent<CollisionObject>(), "hasReturnedToPool"), "이전 적중/반환 상태 초기화");
            Equal(0f, reused.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime, 0.01f);
            yield return 0.12f;
            Require(reused.activeInHierarchy, "수명 이전 활성 상태 유지");
            Equal(0f, Vector2.Angle(Vector2.down, reused.GetComponent<Rigidbody2D>().velocity), 0.01f);
            yield return 0.18f;
            Require(!reused.activeInHierarchy, "설정 수명 종료 후 풀 반환");
            results.Add("PASS 실제 풀 재사용 시 목표/적중/애니메이션 초기화, 유도 비활성 직진, 수명 0.25초 반환");

            // 여러 자식 충돌체에 실제 적중해도 비관통 탄의 피해는 한 번만 적용합니다.
            settings.homingLifetime = 1f;
            GameObject damageProjectile = weapon.SpawnProj(Vector2.right);
            var victim = Target((Vector2)damageProjectile.transform.position + new Vector2(0.65f, 0f));
            for (int i = 0; i < 3; i++)
            {
                var child = new GameObject("자식 피격 영역");
                child.transform.SetParent(victim.transform, false);
                child.tag = "Enemy";
                child.layer = LayerMask.NameToLayer("Enemy");
                child.AddComponent<CircleCollider2D>().radius = 0.4f;
            }
            yield return 0.15f;
            Equal(settings.damage, 10000f - Field<float>(victim, "currentHealth"), 0.01f);
            Require(!damageProjectile.activeInHierarchy, "여러 충돌체에 중복 피해 방지 및 반환");
            var collision = damageProjectile.GetComponent<CollisionObject>();
            int count = pool.poolDictionary[settings.projectilePrefab.name].Count;
            collision.ReturnToPool();
            Require(count == pool.poolDictionary[settings.projectilePrefab.name].Count, "중복 반환 방지");
            results.Add("PASS 실제 Physics2D 적중: 부모 IDamageable 피해 1회, 3개 자식 충돌체 중복 피해/반환 방지");
            victim.gameObject.SetActive(false);

            // 사망 연출 중 활성 상태인 EnemyBase도 선택/추적하지 않습니다.
            var deadObject = new GameObject("사망 검증 적");
            temporary.Add(deadObject);
            deadObject.transform.position = (Vector3)Origin + Vector3.right;
            deadObject.layer = LayerMask.NameToLayer("Enemy");
            var dead = deadObject.AddComponent<EnemyBase>();
            var deadCollider = deadObject.AddComponent<CircleCollider2D>();
            typeof(EnemyBase).GetField("<isDead>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dead, true);
            Physics2D.SyncTransforms();
            Require(HomingProj.FindClosestEnemy(Origin, 10f) == null, "사망한 활성 적 탐색 제외");
            GameObject deathProjectile = weapon.SpawnProj(Vector2.up);
            HomingProj deathMovement = deathProjectile.GetComponent<HomingProj>();
            deathMovement.Initialize(true, 6f, Vector2.up, deadCollider, 180f, 0f, 0f);
            Tick(deathMovement);
            Require(Field<Collider2D>(deathMovement, "targetCollider") == null, "활성 적 사망 시 추적 중단");
            Equal(0f, Vector2.Angle(Vector2.up, deathProjectile.GetComponent<Rigidbody2D>().velocity), 0.01f);
            deathProjectile.GetComponent<CollisionObject>().ReturnToPool();
            results.Add("PASS EnemyBase 사망 연출 중 탐색 제외 및 즉시 추적 중단");
            deadObject.SetActive(false);

            settings.hasHomingProjectiles = true;
            weapon.Initialize(settings);
            weapon.Attack(Vector2.left);
            var noTargetBolts = pool.GetComponentsInChildren<HomingProj>()
                .Where(value => value.gameObject.activeInHierarchy && Vector2.Distance(value.transform.position, Origin) < 2f).ToArray();
            Require(noTargetBolts.Length == 5, "목표 없는 5발 생성");
            var noTargetAngles = noTargetBolts.Select(value => Vector2.SignedAngle(Vector2.left, value.GetComponent<Rigidbody2D>().velocity))
                .OrderBy(value => value).ToArray();
            for (int i = 0; i < 5; i++) Equal(expectedAngles[i], noTargetAngles[i], 0.1f);
            foreach (HomingProj bolt in noTargetBolts) bolt.GetComponent<CollisionObject>().ReturnToPool();
            results.Add("PASS 발사 시 목표 없음: 조준 방향 기준 5발 초기 각도 및 직진");

            settings.homingLifetime = 4f;
            settings.projectileRotationOffset = 0f;
            foreach (float distance in new[] { 0.6f, 1f, 1.5f })
            {
                var closeTarget = Target((Vector2)weapon.firePoint.position + Vector2.right * distance);
                closeTarget.GetComponent<CircleCollider2D>().radius = 0.1f;
                Physics2D.SyncTransforms();
                weapon.Initialize(settings);
                weapon.Attack(Vector2.right);
                yield return 2.5f;
                Equal(settings.damage * 5f, 10000f - Field<float>(closeTarget, "currentHealth"), 0.01f);
                closeTarget.gameObject.SetActive(false);
                results.Add($"PASS 근거리 {distance}: 증가 +4의 5발 모두 적중, 원운동 없이 피해 5회");
            }

            verificationPlayerData.extraProjectiles = 0f;
            var singleTarget = Target((Vector2)weapon.firePoint.position + Vector2.right);
            singleTarget.GetComponent<CircleCollider2D>().radius = 0.1f;
            Physics2D.SyncTransforms();
            weapon.Initialize(settings);
            weapon.Attack(Vector2.left);
            yield return 0.5f;
            Equal(settings.damage, 10000f - Field<float>(singleTarget, "currentHealth"), 0.01f);
            singleTarget.gameObject.SetActive(false);
            results.Add("PASS 근거리 기본 1발: 목표 방향 직진, 피해 1회 및 반환");

            // 이미 목표를 지나친 탄도 회전 제한을 지키면서 돌아와 실제로 적중해야 합니다.
            var behindTarget = Target((Vector2)weapon.firePoint.position + Vector2.right * 0.8f);
            behindTarget.GetComponent<CircleCollider2D>().radius = 0.1f;
            Physics2D.SyncTransforms();
            GameObject recovery = weapon.SpawnProj(Vector2.up);
            recovery.GetComponent<HomingProj>().Initialize(true, 6f, Vector2.up,
                behindTarget.GetComponent<Collider2D>(), 180f, 0.15f, 0f);
            yield return 2.5f;
            Equal(settings.damage, 10000f - Field<float>(behindTarget, "currentHealth"), 0.01f);
            Require(!recovery.activeInHierarchy, "지나친 근거리 목표 적중 후 반환");
            behindTarget.gameObject.SetActive(false);
            results.Add("PASS 근거리에서 90도로 빗나간 탄: 제한 회전/감속 후 실제 적중 및 반환");
        }

        private static HomingProj[] ActiveBolts() => pool.GetComponentsInChildren<HomingProj>()
            .Where(value => value.gameObject.activeInHierarchy && Vector2.Distance(value.transform.position, Origin) < 2f).ToArray();

        private static EnemyBase Target(Vector2 position)
        {
            var root = new GameObject("검증 적");
            temporary.Add(root);
            root.transform.position = position;
            root.tag = "Enemy";
            root.layer = LayerMask.NameToLayer("Enemy");
            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
            var target = root.AddComponent<EnemyBase>();
            typeof(EnemyBase).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, 10000f);
            Physics2D.SyncTransforms();
            return target;
        }

        private static T Field<T>(object instance, string name) =>
            (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        private static void Tick(HomingProj projectile) => typeof(HomingProj)
            .GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(projectile, null);
        private static void Equal(float expected, float actual, float tolerance) =>
            Require(Mathf.Abs(expected - actual) <= tolerance, $"예상 {expected}, 실제 {actual}");
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void Finish(Exception error)
        {
            if (error != null) { results.Add("FAIL " + error); Debug.LogException(error); }
            File.WriteAllLines(HomingMagicSetup.Output + "/verification.txt",
                new[] { "Unity " + Application.unityVersion + " / GameScene Play Mode", error == null ? "PASS" : "FAIL" }.Concat(results));
            if (settings != null && pool != null)
                foreach (var value in pool.GetComponentsInChildren<HomingProj>())
                    if (Vector2.Distance(value.transform.position, Origin) < 30f)
                        value.GetComponent<CollisionObject>().ReturnToPool();
            foreach (GameObject item in temporary) if (item != null) Object.Destroy(item);
            temporary.Clear();
            PoolInstance.SetValue(null, previousPool);
            Time.timeScale = previousTimeScale;
            Application.runInBackground = previousRunInBackground;
            EditorApplication.isPaused = previousPause;
            Physics2D.simulationMode = previousSimulationMode;
            if (settings != null) Object.Destroy(settings);
            if (verificationPlayerData != null) Object.Destroy(verificationPlayerData);
            running = false;
            SessionState.SetBool(Pending + ".ExitAfter", false);
            if (exitAfter) EditorApplication.isPlaying = false;
        }

        private static IEnumerator Execute(IEnumerator checks)
        {
            while (true)
            {
                bool next;
                Exception failure = null;
                try { next = checks.MoveNext(); }
                catch (Exception error) { failure = error; next = false; }
                if (!next)
                {
                    Finish(failure);
                    yield break;
                }
                File.WriteAllLines(HomingMagicSetup.Output + "/verification-progress.txt", results);
                yield return checks.Current is float seconds ? new WaitForSeconds(seconds) : checks.Current;
            }
        }
    }
}
