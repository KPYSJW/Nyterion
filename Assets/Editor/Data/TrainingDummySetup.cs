using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using Nytherion.UI.Test;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>허수아비 자산 생성과 실제 디버그 패널의 플레이 모드 검증 도구입니다.</summary>
    [InitializeOnLoad]
    public static class TrainingDummySetup
    {
        public const string PrefabPath = "Assets/Prefabs/Debug/TrainingDummy.prefab";
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Enemy/TrainingDummy.asset";
        private const string Output = "output/training-dummy";
        private const string Pending = "TrainingDummy.VerifyPending";
        private static readonly List<string> checks = new List<string>();
        private static DebugPanelUI panel;
        private static TrainingDummy target;
        private static EventManager events;
        private static Action<PlayerDamageEventData> hitListener;
        private static Action<EnemyBase> deathListener;
        private static int hitCount, deathCount;
        private static float waitUntil;
        private static double readyAt, timeoutAt;
        private static bool removing;

        static TrainingDummySetup()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                {
                    readyAt = EditorApplication.timeSinceStartup + 2d;
                    timeoutAt = readyAt + 30d;
                }
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/setup.request"))
                {
                    File.Delete(Output + "/setup.request");
                    CreateAssets();
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/verify.request"))
                {
                    File.Delete(Output + "/verify.request");
                    Verify();
                }
                if (!EditorApplication.isPlaying || !SessionState.GetBool(Pending, false) ||
                    EditorApplication.timeSinceStartup < readyAt) return;
                if (removing)
                {
                    if (target != null) return;
                    Require(Object.FindObjectsOfType<TrainingDummy>().Length == 0, "제거 버튼으로 소환한 허수아비 모두 정리");
                    Finish(null);
                }
                else if (target == null)
                {
                    PlayerManager player = Object.FindObjectOfType<PlayerManager>();
                    panel = Object.FindObjectOfType<DebugPanelUI>();
                    if (player == null || player.currentPlayerData == null || player.EventManager == null || panel == null)
                    {
                        if (EditorApplication.timeSinceStartup > timeoutAt)
                            throw new InvalidOperationException("플레이어 및 디버그 패널 초기화 시간 초과");
                        return;
                    }
                    BeginChecks(player);
                }
                else if (Time.time >= waitUntil)
                {
                    Require(hitCount > 102, "화상 지속 피해도 반복 적용되며 계속 생존");
                    Require(!target.isDead && target.gameObject.activeInHierarchy && deathCount == 0,
                        "직접 피해와 지속 피해 뒤에도 사망 이벤트 및 풀 반환 없음");
                    panel.Open(false);
                    panel.GetComponentsInChildren<Button>(true)
                        .First(button => button.name == "RemoveTrainingDummiesButton").onClick.Invoke();
                    panel.Close();
                    removing = true;
                }
            }
            catch (Exception exception)
            {
                if (SessionState.GetBool(Pending, false)) Finish(exception);
                else
                {
                    Directory.CreateDirectory(Output);
                    File.WriteAllText(Output + "/setup.txt", "FAIL " + exception);
                    Debug.LogException(exception);
                }
            }
        }

        [MenuItem("Tools/Nytherion/Training Dummy/Create Assets And Connect Panel")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Gameplay/Characters/Player/Player.prefab");
            SpriteRenderer playerSprite = playerPrefab.GetComponent<SpriteRenderer>();
            CapsuleCollider2D playerBody = playerPrefab.GetComponent<CapsuleCollider2D>();
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                data.enemyName = "허수아비";
                // 체력 소모는 TrainingDummy가 생략하므로 큰 수나 Infinity를 저장하지 않습니다.
                data.maxHealth = 1f;
                data.moveSpeed = 0f;
                data.damageAmount = 0;
                data.detectRange = 0f;
                data.dropChance = 0f;
                AssetDatabase.CreateAsset(data, DataPath);
            }

            GameObject root = new GameObject("TrainingDummy");
            try
            {
                root.tag = "Enemy";
                root.layer = LayerMask.NameToLayer("Enemy");
                root.transform.localScale = playerPrefab.transform.localScale;
                SpriteRenderer sprite = root.AddComponent<SpriteRenderer>();
                EditorUtility.CopySerialized(playerSprite, sprite);
                CapsuleCollider2D body = root.AddComponent<CapsuleCollider2D>();
                body.size = playerBody.size;
                body.offset = playerBody.offset;
                body.direction = playerBody.direction;
                Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
                rigidbody.bodyType = RigidbodyType2D.Kinematic;
                rigidbody.gravityScale = 0f;
                rigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
                root.AddComponent<StatusEffectManager>();
                TrainingDummy dummy = root.AddComponent<TrainingDummy>();
                dummy.enemyData = data;
                SerializedObject dummyObject = new SerializedObject(dummy);
                dummyObject.FindProperty("spriteRenderer").objectReferenceValue = sprite;
                dummyObject.FindProperty("groundBodyCollider").objectReferenceValue = body;
                dummyObject.ApplyModifiedPropertiesWithoutUndo();
                EnemyHitAreaSetup.FitToSprite(dummy);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                data.enemyPrefab = prefab;
                EditorUtility.SetDirty(data);
            }
            finally { Object.DestroyImmediate(root); }

            TrainingDummy source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<TrainingDummy>();
            int connected = 0;
            Directory.CreateDirectory(Output);
            foreach (DebugPanelUI debugPanel in Object.FindObjectsOfType<DebugPanelUI>(true))
            {
                if (debugPanel.gameObject.scene.path != "Assets/Scenes/GameScene.unity") continue;
                string backup = Output + "/GameScene-before-training-dummy.unity";
                if (!File.Exists(backup)) EditorSceneManager.SaveScene(debugPanel.gameObject.scene, backup, true);
                SerializedObject serializedPanel = new SerializedObject(debugPanel);
                serializedPanel.FindProperty("trainingDummyPrefab").objectReferenceValue = source;
                serializedPanel.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(debugPanel.gameObject.scene);
                EditorSceneManager.SaveScene(debugPanel.gameObject.scene);
                connected++;
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output + "/setup.txt", $"PASS 프리팹/적 데이터 생성, GameScene 디버그 패널 {connected}개 연결");
        }

        [MenuItem("Tools/Nytherion/Training Dummy/Verify In Play Mode")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            checks.Clear();
            hitCount = deathCount = 0;
            target = null;
            removing = false;
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void BeginChecks(PlayerManager player)
        {
            panel.Open(false);
            Button spawnButton = panel.GetComponentsInChildren<Button>(true)
                .First(button => button.name == "SpawnTrainingDummyButton");
            Canvas.ForceUpdateCanvases();
            foreach (Button button in panel.GetComponentsInChildren<Button>(true)
                .Where(button => button.name == "SpawnTrainingDummyButton" || button.name == "RemoveTrainingDummiesButton"))
            {
                Vector3[] corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                RectTransform buttonArea = (RectTransform)button.transform.parent;
                Vector2 min = buttonArea.InverseTransformPoint(corners[0]);
                Vector2 max = buttonArea.InverseTransformPoint(corners[2]);
                Require(buttonArea.rect.Contains(min) && buttonArea.rect.Contains(max),
                    $"{button.name} 패널 버튼 영역 안에 표시 (범위 {min} ~ {max})");
            }
            foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                typeof(DebugPanelUI).GetField("trainingDummySpawnDirection", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(panel, direction);
                Vector3 expectedPosition = player.transform.position + (Vector3)(direction * 1.5f);
                spawnButton.onClick.Invoke();
                target = ((List<TrainingDummy>)typeof(DebugPanelUI)
                    .GetField("trainingDummies", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel)).Last();
                Require(Vector3.Distance(target.transform.position,
                    expectedPosition) < 0.01f,
                    $"디버그 버튼으로 {direction} 방향 플레이어 앞 1.5m 소환 (실제 {target.transform.position}, 기대 {expectedPosition})");
                if (direction != Vector2.down)
                {
                    panel.RemoveTrainingDummies();
                    Object.DestroyImmediate(target.gameObject);
                }
            }
            panel.Close();
            Require(target.CompareTag("Enemy") && target.gameObject.layer == LayerMask.NameToLayer("Enemy"),
                "기존 공격의 Enemy 태그 및 레이어 판정 호환");
            Require(target.GetComponent<EnemyAIController>() == null &&
                target.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic,
                "이동과 공격 없이 제자리에 유지");
            Sprite expected = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Gameplay/Characters/Player/Player.prefab").GetComponent<SpriteRenderer>().sprite;
            Require(target.GetComponent<SpriteRenderer>().sprite == expected, "기존 플레이어 이미지 사용");
            Require(((IGroundDamageable)target).TryGetGroundHitCircle(out Vector2 center, out float radius) && radius > 0f,
                "지면 공격용 피격 원 제공");
            Physics2D.SyncTransforms();
            Require(Physics2D.OverlapCircleAll(center, radius, 1 << target.gameObject.layer)
                .Any(hit => hit.GetComponent<IDamageable>() == target), "Physics2D 검색에서 피해 대상으로 인식");
            Require(target.GetComponent<StatusEffectManager>().PlayerManager == player,
                "씬 DI가 허수아비 전투 및 상태 효과 컨텍스트에 주입됨");

            events = player.EventManager;
            hitListener = hit => { if (hit.Target == target) hitCount++; };
            deathListener = dead => { if (dead == target) deathCount++; };
            events.OnEnemyDamagedByPlayerDetailed += hitListener;
            events.OnEnemyDied += deathListener;
            float originalCritChance = player.currentPlayerData.critChance;
            float originalCritMultiplier = player.currentPlayerData.critDamageMultiplier;
            PlayerDamageEventData lastHit = default;
            Action<PlayerDamageEventData> criticalListener = hit => { if (hit.Target == target) lastHit = hit; };
            events.OnEnemyDamagedByPlayerDetailed += criticalListener;
            try
            {
                player.currentPlayerData.critChance = 1f;
                player.currentPlayerData.critDamageMultiplier = 2f;
                target.TakeDamage(10f, true);
                Require(lastHit.IsCritical && Mathf.Approximately(lastHit.DamageAmount, 20f), "일반 적과 같은 치명타 배율 및 상세 피해 이벤트");
            }
            finally
            {
                player.currentPlayerData.critChance = originalCritChance;
                player.currentPlayerData.critDamageMultiplier = originalCritMultiplier;
                events.OnEnemyDamagedByPlayerDetailed -= criticalListener;
            }
            hitCount = 0;
            for (int i = 0; i < 100; i++) target.TakeDamage(1000000f, true);
            target.TakeDamage(float.MaxValue, true);
            target.TakeDamage(1f, true);
            Require(hitCount == 102 && !target.isDead && target.gameObject.activeInHierarchy,
                "최대 float 피해 포함 102회 타격 후에도 모든 피격 이벤트 수신 및 생존");
            Require(target.GetComponent<SpriteRenderer>().color == Color.red, "일반 적과 같은 피격 색상 표시");
            target.GetComponent<StatusEffectManager>().ApplyEffect(new FireEffect(5f, 2f));
            waitUntil = Time.time + 0.8f;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add("PASS " + message);
        }

        private static void Finish(Exception exception)
        {
            if (events != null)
            {
                events.OnEnemyDamagedByPlayerDetailed -= hitListener;
                events.OnEnemyDied -= deathListener;
            }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/verification.txt", string.Join("\n", checks) +
                (exception == null ? "\nPASS 전체 검증 완료" : "\nFAIL " + exception));
            SessionState.SetBool(Pending, false);
            target = null;
            if (exception != null) Debug.LogException(exception);
            EditorApplication.isPlaying = false;
        }
    }
}
