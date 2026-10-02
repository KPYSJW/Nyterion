using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Gameplay.Relics.Modules;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class WindStoneRelicVerification
    {
        private const string Pending = "Nytherion.WindStone.Verify";
        private static readonly FieldInfo PoolInstance = typeof(ObjectPoolManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly List<string> results = new List<string>();
        private static ObjectPoolManager pool, previousPool;
        private static GameObject playerRoot, enemyRoot;
        private static IObjectResolver resolver;
        private static WindStoneRelicEffect effect;
        private static PlayerManager player;
        private static bool running;
        private static float previousTimeScale;
        private static bool previousBackground;
        private static double readyAt;

        static WindStoneRelicVerification()
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
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || running) return;
            if (File.Exists(WindStoneRelicSetup.Output + "/verify.request"))
            {
                File.Delete(WindStoneRelicSetup.Output + "/verify.request");
                Start();
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || readyAt == 0 || EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            running = true;
            try
            {
                results.Clear();
                previousTimeScale = Time.timeScale;
                previousBackground = Application.runInBackground;
                Application.runInBackground = true;
                Time.timeScale = 1f;
                previousPool = ObjectPoolManager.Instance;
                PoolInstance.SetValue(null, null);
                pool = new GameObject("[풍석 검증] 전용 풀").AddComponent<ObjectPoolManager>();
                pool.Initialize();
                pool.StartCoroutine(Execute(Verify()));
            }
            catch (Exception error) { Finish(error); }
        }

        [MenuItem("Tools/Nytherion/Wind Stone/Verify In Play Mode")]
        public static void Start()
        {
            SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
            SessionState.SetBool(Pending, true);
            readyAt = EditorApplication.timeSinceStartup + 1d;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        public static void RunBatch()
        {
            Directory.CreateDirectory(WindStoneRelicSetup.Output);
            // 복제 프로젝트의 검증 실행은 실제 플레이어 저장 경로를 사용하지 않습니다.
            PlayerSettings.productName = "NytherionWindStoneVerification";
            WindStoneRelicSetup.CreateAssets();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            SessionState.SetBool(Pending + ".Batch", true);
            Start();
        }

        private static IEnumerator Execute(IEnumerator steps)
        {
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) break; next = steps.Current; }
                catch (Exception error) { Finish(error); yield break; }
                yield return next;
            }
            Finish(null);
        }

        private static IEnumerator Verify()
        {
            var data = AssetDatabase.LoadAssetAtPath<RelicData>(WindStoneRelicSetup.RelicPath);
            Require(data != null && data.koreanName == "풍석" && data.Image != null, "아이콘/유물 데이터");
            foreach (var name in new[] { "WindStoneProj", "WindStoneProjHitEffect" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WindStoneRelicSetup.AnimationFolder + "/" + name + ".anim");
                int count = name == "WindStoneProj" ? 5 : 4;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
                Require(keys.Take(count).Select(k => k.value).Distinct().Count() == count, name + " 프레임");
                Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime == (count == 5), name + " 루프");
                Require(Mathf.Abs(clip.length - count / 12f) < 0.001f, name + " 프레임 유지 시간");
            }
            results.Add("PASS 아이콘 참조, 5프레임 12fps 루프/4프레임 12fps 단발 및 마지막 프레임 유지 시간");
            var builder = new ContainerBuilder();
            builder.RegisterInstance(pool);
            resolver = builder.Build();
            playerRoot = new GameObject("[풍석 검증] 플레이어");
            playerRoot.transform.position = new Vector3(10000f, 10000f);
            player = playerRoot.AddComponent<PlayerManager>();
            var events = playerRoot.AddComponent<EventManager>();
            player.Construct(null, null, events, null, null, resolver);
            Require(player.ObjectPool == pool, "DI 풀 접근");
            enemyRoot = new GameObject("[풍석 검증] 적");
            enemyRoot.tag = "Enemy";
            enemyRoot.transform.position = playerRoot.transform.position;
            var enemy = enemyRoot.AddComponent<EnemyBase>();
            typeof(EnemyBase).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 10000f);
            enemy.Construct(events, null, null);
            var collider = enemyRoot.AddComponent<CircleCollider2D>();
            collider.radius = 5f;
            collider.isTrigger = true;
            effect = new WindStoneRelicEffect
            {
                projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindStoneRelicSetup.ProjectilePath),
                procChance = 0f,
                spawnRadius = 0f
            };
            effect.ApplyEffect(player, 1);
            events.TriggerEnemyDamagedByPlayerWithCrit(20f, false, enemy, false);
            Require(ActiveProjectiles().Length == 0, "0% 확률");
            effect.procChance = 1f;
            events.TriggerEnemyDamagedByPlayerWithCrit(20f, false, enemy, true);
            Require(ActiveProjectiles().Length == 0, "연쇄 피해 제외");
            events.TriggerEnemyDamagedByPlayerWithCrit(20f, false, enemy, false);
            var projectile = ActiveProjectiles().Single();
            Require(Mathf.Abs(projectile.damage - 10f) < 0.001f, "1레벨 50% 피해");
            Require(Mathf.Abs(projectile.GetComponent<Rigidbody2D>().velocity.magnitude - 6f) < 0.001f, "초기 속도");
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(0.65f);
            Require(projectile.gameObject.activeInHierarchy, "다중 타격 중 생존");
            Require(Mathf.Abs(projectile.GetComponent<Rigidbody2D>().velocity.magnitude - 1f) < 0.001f, "충돌 후 감속");
            float health = (float)typeof(EnemyBase).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);
            Require(health <= 9970f, "최초 적중과 두 번 이상 반복 피해");
            Require(ActiveProjectiles().Length == 1, "풍석 피해의 재귀 발동 방지");
            Require(pool.poolDictionary.ContainsKey("WindStoneProjHitEffect"), "실제 타격 효과 풀 생성");
            results.Add("PASS DI 풀, 0%/100% 발동, 초기 속도 6 → 충돌 속도 1, 실제 Physics2D 최초 타격 및 반복 타격, 타격 VFX 및 재귀 방지");
            yield return new WaitForSeconds(1.1f);
            Require(!projectile.gameObject.activeInHierarchy, "충돌 후 수명 반환");
            collider.enabled = false;
            effect.ApplyEffect(player, 3);
            events.TriggerEnemyDamagedByPlayerWithCrit(20f, false, enemy, false);
            var leveled = ActiveProjectiles().Single();
            Require(Mathf.Abs(leveled.damage - 14f) < 0.001f, "3레벨 70% 피해");
            leveled.ReturnToPool();
            effect.RemoveEffect(player, 3);
            events.TriggerEnemyDamagedByPlayerWithCrit(20f, false, enemy, false);
            Require(ActiveProjectiles().Length == 0, "제거 후 이벤트 해제");
            // 동일 풀의 모든 인스턴스를 순환시켜 최초 사용 인스턴스의 상태 초기화를 확인합니다.
            GameObject reused = null;
            for (int i = 0; i < 11; i++)
            {
                GameObject spawned = pool.SpawnFromPool(effect.projectilePrefab, playerRoot.transform.position, Quaternion.identity);
                if (spawned == projectile.gameObject) { reused = spawned; break; }
                spawned.GetComponent<CollisionObject>().ReturnToPool();
            }
            Require(reused != null, "동일 인스턴스 재사용");
            yield return new WaitForSeconds(1.6f);
            Require(reused.activeInHierarchy, "다시 발사한 미적중 탄의 이전 수명 초기화");
            yield return new WaitForSeconds(4.6f);
            Require(!reused.activeInHierarchy, "미적중 탄 최대 수명 반환");
            Require(pool.GetComponentsInChildren<Animator>(true).Where(a => a.name == "WindStoneProjHitEffect(Clone)").All(a => !a.gameObject.activeInHierarchy), "타격 효과 애니메이션 종료 반환");
            results.Add("PASS 1.5초 충돌 수명, 레벨 피해 증가, 재적용 구독 중복 방지/해제, 실제 풀 재사용 상태 초기화, 미적중 탄 6초 반환, 타격 효과 반환");
        }

        private static CollisionObject[] ActiveProjectiles() => pool.GetComponentsInChildren<CollisionObject>().Where(c => c.poolTag == "WindStoneProj").ToArray();
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        private static void Finish(Exception error)
        {
            if (error != null) results.Add(error.ToString());
            File.WriteAllLines(WindStoneRelicSetup.Output + "/verification.txt", new[] { "Unity " + Application.unityVersion + " / GameScene Play Mode", error == null ? "PASS" : "FAIL" }.Concat(results));
            effect?.RemoveEffect(player, 1);
            if (enemyRoot != null) Object.Destroy(enemyRoot);
            if (playerRoot != null) Object.Destroy(playerRoot);
            if (pool != null) Object.Destroy(pool.gameObject);
            PoolInstance.SetValue(null, previousPool);
            resolver?.Dispose();
            Time.timeScale = previousTimeScale;
            Application.runInBackground = previousBackground;
            running = false;
            if (SessionState.GetBool(Pending + ".Batch", false))
            {
                EditorApplication.Exit(error == null ? 0 : 1);
                return;
            }
            if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
        }
    }
}
