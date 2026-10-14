using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.UI.Test;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class DebugPanelStatsVerification
    {
        private const string Output = "output/debug-stats";
        private const string Pending = "Nytherion.DebugStats.Verify";
        private static double readyAt;

        static DebugPanelStatsVerification()
        {
            EditorApplication.update += Update;
        }

        [MenuItem("Tools/Nytherion/Debug Panel/Verify Stats In Play Mode")]
        public static void Start()
        {
            SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
            SessionState.SetBool(Pending, true);
            readyAt = EditorApplication.timeSinceStartup + 5d;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/verify.request"))
            {
                File.Delete(Output + "/verify.request");
                Start();
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (readyAt == 0d) readyAt = EditorApplication.timeSinceStartup + 5d;
            if (EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            Directory.CreateDirectory(Output);
            try
            {
                Verify();
                File.WriteAllText(Output + "/verification.txt",
                    "PASS: GameScene 플레이 모드 능력치 버튼 16개, 증가/반복 클릭, %p 증가, 투사체 수 증가, 초기화, 외부 보정 유지, 패널 재열기, 패널 내부 배치.");
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL: " + error);
                Debug.LogException(error);
            }
            finally
            {
                if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
                readyAt = 0d;
            }
        }

        private static void Verify()
        {
            DebugPanelUI panel = UnityEngine.Object.FindObjectsOfType<DebugPanelUI>(true)
                .FirstOrDefault(p => p.gameObject.scene.name == "GameScene");
            Require(panel != null, "GameScene의 DebugPanelUI가 없습니다.");
            FieldInfo playerField = typeof(DebugPanelUI).GetField("playerManager", BindingFlags.Instance | BindingFlags.NonPublic);
            PlayerManager originalPlayer = (PlayerManager)playerField.GetValue(panel);
            bool wasOpen = panel.IsOpen;
            GameObject root = new GameObject("[검증] 임시 능력치 플레이어");
            PlayerData data = ScriptableObject.CreateInstance<PlayerData>();
            PlayerManager player = root.AddComponent<PlayerManager>();
            try
            {
                foreach (FieldInfo field in typeof(PlayerData).GetFields())
                    if (field.FieldType == typeof(float)) field.SetValue(data, 1f);
                typeof(PlayerManager).GetField("basePlayerData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, data);
                player.Initialize();
                playerField.SetValue(panel, player);
                StatModifier external = new StatModifier { stat = StatType.Defense, value = 3f };
                player.AddTemporaryStatModifier(external);
                string baseline = JsonUtility.ToJson(player.currentPlayerData);
                panel.Open(false);
                GameObject content = (GameObject)typeof(DebugPanelUI).GetField("contentPanel2", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                Require(content != null && content.activeInHierarchy, "ContentPanel2 활성화 실패");
                Button[] buttons = content.GetComponentsInChildren<Button>(true);
                Require(buttons.Length == 17, "능력치 16개와 초기화 버튼이 필요합니다.");
                foreach (StatType stat in Enum.GetValues(typeof(StatType)))
                {
                    if (stat == StatType.All) continue;
                    string fieldName = stat == StatType.CritDamage ? "critDamageMultiplier"
                        : stat == StatType.ProjectileSize ? "projectileSizeMultiplier"
                        : char.ToLowerInvariant(stat.ToString()[0]) + stat.ToString().Substring(1);
                    FieldInfo field = typeof(PlayerData).GetField(fieldName);
                    float before = (float)field.GetValue(player.currentPlayerData);
                    Button button = buttons.Single(b => b.name == "IncreaseStat_" + stat);
                    Require(button.onClick.GetPersistentEventCount() == 0, "원본 버튼의 영구 이벤트가 복제됐습니다.");
                    button.onClick.Invoke();
                    float after = (float)field.GetValue(player.currentPlayerData);
                    Require(after > before, stat + " 증가 실패");
                    if (stat == StatType.CritChance) Require(Mathf.Approximately(after - before, 0.05f), "치명타 5%p 증가 실패");
                    if (stat == StatType.ExtraProjectiles) Require(Mathf.Approximately(after - before, 1f), "투사체 1개 증가 실패");
                }
                float damage = player.currentPlayerData.meleeDamage;
                buttons.Single(b => b.name == "IncreaseStat_MeleeDamage").onClick.Invoke();
                Require(player.currentPlayerData.meleeDamage > damage, "반복 클릭 누적 실패");
                panel.Close();
                panel.Open(false);
                Require(content.GetComponentsInChildren<Button>(true).Length == 17, "패널 재열기 시 중복 버튼 발생");
                Canvas.ForceUpdateCanvases();
                RectTransform container = (RectTransform)content.transform.Find("Buttons");
                LayoutRebuilder.ForceRebuildLayoutImmediate(container);
                foreach (Button button in buttons)
                {
                    Vector3[] corners = new Vector3[4];
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 local = container.InverseTransformPoint(corner);
                        Rect bounds = container.rect;
                        Require(local.x >= bounds.xMin - 0.01f && local.x <= bounds.xMax + 0.01f &&
                            local.y >= bounds.yMin - 0.01f && local.y <= bounds.yMax + 0.01f,
                            $"패널 밖 버튼: {button.name}, 위치 {local}, 컨테이너 {bounds}");
                    }
                }
                buttons.Single(b => b.name == "ResetDebugStatsButton").onClick.Invoke();
                Require(JsonUtility.ToJson(player.currentPlayerData) == baseline, "초기화 또는 외부 보정 유지 실패");
                Require(Mathf.Approximately(data.defense, 1f), "원본 능력치 데이터가 수정됐습니다.");
            }
            finally
            {
                panel.ResetDebugStats();
                playerField.SetValue(panel, originalPlayer);
                if (!wasOpen) panel.Close();
                UnityEngine.Object.DestroyImmediate(player.currentPlayerData);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
