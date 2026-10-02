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
