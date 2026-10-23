using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Core.Data;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Gameplay.Relics.Modules;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Enemy.States;
using Nytherion.GamePlay.Characters.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class RuntimeReviewVerification
    {
        private const string Output = "output/runtime-review";
        static RuntimeReviewVerification() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/verify.request")) return;
            File.Delete(Output + "/verify.request");
            Verify();
        }

        [MenuItem("Tools/Nytherion/Review/Verify Movement And Pool Lookup")]
        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            var temporary = new List<Object>();
            Scene scene = EditorSceneManager.NewPreviewScene();
            ObjectPoolManager previousPool = ObjectPoolManager.Instance;
            FieldInfo poolInstance = typeof(ObjectPoolManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                var root = new GameObject("이동 검증");
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                var enemy = root.AddComponent<EnemyAIController>();
                enemy.Root = root.transform;
                enemy.agent = root.AddComponent<NavMeshAgent>();
                enemy.player = root.transform;
                var movement = new MovementProbe();
                Set(enemy, "movementBehavior", movement);
                var state = new StateProbe(enemy);
                Set(enemy, "currentState", state);

                state.Tick = target => target.MoveTowardsPlayer();
                Tick(enemy);
                Require(movement.Moves == 1 && movement.DirectionReads == 1, "추적 상태의 이동과 방향 갱신 각 1회");
                movement.Reset();
                enemy.SetForcedDestination(new Vector3(8, 9));
                state.Tick = target => { target.MoveTowardsPlayer(); target.TryMoveToForcedDestination(); target.MoveToTarget(Vector2.left); };
                Tick(enemy);
                Require(movement.Moves == 1 && movement.Last == new Vector3(8, 9) && movement.DirectionReads == 1,
                    "상태 내 여러 요청에도 강제 목적지 우선·이동 1회");
                movement.Reset();
                enemy.ClearForcedDestination();
                state.Tick = target => { target.MoveInDirection(Vector2.right); target.StopMovement(); };
                Tick(enemy);
                Require(movement.Moves == 0 && movement.Stops == 1, "상태 전환의 정지는 앞선 이동 요청 취소");
                movement.Reset();
                enemy.SetMovementAllowed(false);
                state.Tick = target => { target.MoveTowardsPlayer(); target.TryMoveToForcedDestination(); };
                enemy.SetForcedDestination(Vector3.one);
                Tick(enemy);
                Require(movement.Moves == 0, "이동 금지 중 강제·일반 이동 차단");
                enemy.SetMovementAllowed(true);
                enemy.ClearForcedDestination();
                movement.Reset();
                enemy.MoveToTarget(Vector2.up);
                Require(movement.Moves == 1 && movement.Last == Vector3.up, "상태 갱신 밖의 공개 이동 API는 즉시 적용");
                enemy.PrepareForPoolReturn();
                Require(!(bool)Get(enemy, "hasForcedDestination") && !(bool)Get(enemy, "hasPendingDestination"), "풀 반환 시 이동 요청 잔류 없음");
                results.Add("PASS 이동·방향 중복, 강제 이동 우선순위, 정지·금지·외부 API·풀 반환 6개 시나리오");

                poolInstance.SetValue(null, null);
                var poolRoot = new GameObject("풀 검증");
                SceneManager.MoveGameObjectToScene(poolRoot, scene);
                var pool = poolRoot.AddComponent<ObjectPoolManager>();
                var prefab = new GameObject("검증 프리팹");
                SceneManager.MoveGameObjectToScene(prefab, scene);
                prefab.transform.localScale = new Vector3(2, 3, 4);
                prefab.SetActive(false);
                pool.pools = new List<ObjectPoolManager.Pool> { new ObjectPoolManager.Pool { tag = "지정 태그", prefab = prefab, size = 1 } };
                pool.Initialize();
                var first = pool.SpawnFromPool("지정 태그", Vector3.one, Quaternion.identity);
                var expanded = pool.SpawnFromPool("지정 태그", Vector3.zero, Quaternion.identity);
                Require(first != null && expanded != null && first != expanded, "초기 풀과 소진 후 확장");
                Require(expanded.transform.localScale == prefab.transform.localScale, "확장 시 원본 크기 유지");
                pool.ReturnToPool("잘못된 외부 태그", first);
                first.transform.localScale = Vector3.one;
                Require(pool.SpawnFromPool("지정 태그", Vector3.right, Quaternion.identity) == first &&
                    first.transform.localScale == prefab.transform.localScale, "등록 태그로 반환·인스턴스 재사용·크기 복구");
                var dynamic = pool.SpawnFromPool(prefab, Vector3.zero, Quaternion.identity, 1);
                Require(dynamic != null && pool.pools.Count == 2, "프리팹 기반 동적 풀 생성 및 설정 등록");
                pool.ReturnToPool(prefab.name, dynamic);
                Require(pool.SpawnFromPool(prefab.name, Vector3.zero, Quaternion.identity) == dynamic, "동적 풀도 태그 조회로 재사용");
                results.Add("PASS 직렬화 풀·동적 풀 생성, 소진 후 확장, 태그 보정 반환, 원본 크기·동일 인스턴스 재사용");

                // 생성·활성화 비용과 분리해 조회 부분만 계측한다. 전체 게임 성능의 지표는 아니다.
                var configurations = new List<ObjectPoolManager.Pool>();
                var lookup = new Dictionary<string, ObjectPoolManager.Pool>();
                for (int i = 0; i < 128; i++)
                {
                    var entry = new ObjectPoolManager.Pool { tag = "풀" + i };
                    configurations.Add(entry); lookup.Add(entry.tag, entry);
                }
                string key = configurations[127].tag;
                Predicate<ObjectPoolManager.Pool> predicate = entry => entry.tag == key;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 100000; i++) configurations.Find(predicate);
                double linear = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                for (int i = 0; i < 100000; i++) lookup.TryGetValue(key, out _);
                results.Add($"INFO 풀 128개·마지막 태그 10만 회 조회: List.Find {linear:F2}ms / Dictionary {watch.Elapsed.TotalMilliseconds:F2}ms (조회만 계측)");
                VerifyRelics(scene, temporary);
                results.Add("PASS 유물 저장 복원·레벨·비활성화·재활성화·해제·직접 API: 최종 알림 1회, 중복 적용·중간 체력 알림 평가 없음");
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (Object asset in temporary) if (asset != null) Object.DestroyImmediate(asset);
                poolInstance.SetValue(null, previousPool);
            }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        private static void VerifyRelics(Scene scene, List<Object> temporary)
        {
            var root = new GameObject("유물 중복 평가 검증");
            SceneManager.MoveGameObjectToScene(root, scene);
            var global = root.AddComponent<RelicManager>();
            Call(global, "Awake");
            var player = root.AddComponent<PlayerManager>();
            var stats = ScriptableObject.CreateInstance<PlayerData>();
            temporary.Add(stats);
            player.currentPlayerData = stats;
            var relics = root.AddComponent<PlayerRelicManager>();
            var effects = root.AddComponent<RelicEffectController>();
            Call(relics, "Awake");
            Call(effects, "Awake");
            relics.Construct(null, global);
            effects.Construct(global);
            Call(relics, "Start");
            Call(effects, "Start");
            int effectNotifications = 0;
            relics.OnRelicEffectsChanged += () => effectNotifications++;
            relics.OnRelicsChanged += () => typeof(RelicEffectController).GetMethod("HandleHealthChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effects, new object[] { 10f, 20f });
            var source = ScriptableObject.CreateInstance<RelicData>();
            source.relicName = "ReviewProbe";
            source.effectModules = new List<RelicEffectModule>
            {
                new RelicEffectModule { condition = new ConditionProbe(), effects = new List<RelicEffectBase> { new EffectProbe() } }
            };
            temporary.Add(source);
            var chainKey = typeof(RelicEffectController).GetMethod("GetChainTierGroup", BindingFlags.Static | BindingFlags.NonPublic);
            object nullSeries = chainKey.Invoke(null, new object[] { source, new ChainSynergyCondition { targetSeriesId = null } });
            object emptySeries = chainKey.Invoke(null, new object[] { source, new ChainSynergyCondition { targetSeriesId = "" } });
            object otherMeasure = chainKey.Invoke(null, new object[] { source, new ChainSynergyCondition
                { targetSeriesId = "", measure = ChainSynergyMeasure.EquippedSeriesRelicCount } });
            Require(nullSeries.Equals(emptySeries) && !nullSeries.Equals(otherMeasure), "기존 빈 시리즈 그룹 유지, 측정 방식 구분");
            Set(global, "relicDatabase", new Dictionary<string, RelicData> { { source.relicName, source } });
            var data = new SaveData { relicData = new RelicGridState() };
            data.relicData.placedBlocks.Add(new RelicGridState.SavedRelicBlock { relicId = source.relicName, gridRow = 0, gridCol = 0 });
            ConditionProbe.Checks = EffectProbe.Applies = EffectProbe.Removes = 0;
            global.LoadFromSaveData(data);
            Require(effectNotifications == 1 && ConditionProbe.Checks == 1 && EffectProbe.Applies == 1, "로드 최종 상태 1회 평가·적용");
            var placed = relics.GetCurrentRelics()[0];
            temporary.Add(placed);
            placed.level = 2;
            Notify(global);
            Require(effectNotifications == 2 && ConditionProbe.Checks == 2 && EffectProbe.Applies == 2 && EffectProbe.Removes == 1,
                "목록이 같아도 레벨 변경 시 이전 효과 제거 후 한 번 적용");
            placed.isDisabled = true;
            Notify(global);
            Require(effectNotifications == 3 && EffectProbe.Removes == 2 && EffectProbe.Applies == 2, "비활성화 즉시 제거");
            placed.isDisabled = false;
            Notify(global);
            Require(effectNotifications == 4 && EffectProbe.Applies == 3 && ConditionProbe.Checks == 3, "재활성화 즉시 복구");
            global.LoadFromSaveData(new SaveData { relicData = new RelicGridState() });
            Require(effectNotifications == 5 && relics.equippedRelics.Count == 0 && EffectProbe.Removes == 3, "빈 세이브 복원 시 잔류 효과 제거");
            relics.AddRelic(source);
            Require(effectNotifications == 6 && EffectProbe.Applies == 4 && ConditionProbe.Checks == 4, "직접 장착 API 1회 평가");
            relics.RemoveRelic(0);
            Require(effectNotifications == 7 && EffectProbe.Removes == 4, "직접 해제 API 1회 제거");
        }

        [Serializable] private sealed class ConditionProbe : RelicConditionBase
        {
            public static int Checks;
            public override bool IsConditionMet(PlayerManager player, int level) { Checks++; return true; }
        }
        [Serializable] private sealed class EffectProbe : RelicEffectBase
        {
            public static int Applies, Removes;
            public override void ApplyEffect(PlayerManager player, int level) => Applies++;
            public override void RemoveEffect(PlayerManager player, int level) => Removes++;
        }
        private static void Notify(RelicManager manager) => ((Action)Get(manager, "OnRelicStateChanged"))?.Invoke();
        private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private sealed class MovementProbe : IEnemyMovementBehavior
        {
            public int Moves, Stops, DirectionReads;
            public Vector3 Last;
            public bool RequiresNavMeshAgent => false;
            public Vector3 CurrentVelocity { get { DirectionReads++; return Vector3.right; } }
            public void MoveTowards(Vector3 destination) { Moves++; Last = destination; }
            public void StopMovement() => Stops++;
            public void Configure(EnemyData data) { }
            public void ResetForReuse() { }
            public void Reset() { Moves = Stops = DirectionReads = 0; }
        }

        private sealed class StateProbe : EnemyBaseState
        {
            public Action<EnemyAIController> Tick;
            public StateProbe(EnemyAIController enemy) : base(enemy) { }
            public override void EnterState(EnemyAIController enemy) { }
            public override void ExitState(EnemyAIController enemy) { }
            public override void UpdateState(EnemyAIController enemy) => Tick(enemy);
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Tick(EnemyAIController enemy) => typeof(EnemyAIController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
