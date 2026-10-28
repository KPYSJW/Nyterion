using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.UI.Components;
using Nytherion.UI.Inventory;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ManaPresentationSetup
    {
        private const string Output = "output/player-mana";
        // 최대 마나 100과 초당 회복 5를 기준으로 반복 공격·소환·광역기의 비용을 구분합니다.
        private static readonly Dictionary<string, int> Costs = new Dictionary<string, int>
        {
            { "All_Stat_Skill", 40 }, { "Blackhole_Skill", 35 }, { "BubbleShield_Skill", 30 },
            { "ChainIgnition_Skill", 15 }, { "Decoy_Skill", 25 }, { "Lifesteal_Skill", 25 },
            { "MeteorStrike_Skill", 60 }, { "MultiShot_Skill", 10 }, { "Overdrive_Skill", 40 },
            { "PortalGate_Skill", 20 }, { "ProjectileGate_Skill", 30 }, { "PyroTank_Skill", 25 },
            { "Shadow_Clone_Skill", 30 }, { "Soul_Eater_Skill", 10 }, { "Spiral_Skill", 20 },
            { "Turret_Skill", 20 }
        };

        static ManaPresentationSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/current-only.request"))
            {
                File.Delete(Output + "/current-only.request");
                const string path = "Assets/Scenes/GameScene.unity";
                Scene scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (scene.isDirty) Require(EditorSceneManager.SaveScene(scene,
                        Output + "/GameScene-before-current-text-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true), "씬 사본 저장 실패");
                    HPBarUI hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HPBarUI>(true)).Single();
                    var properties = new SerializedObject(hud);
                    TextMeshProUGUI text = (TextMeshProUGUI)properties.FindProperty("mpText").objectReferenceValue;
                    text.text = "100";
                    var report = new System.Text.StringBuilder();
                    VerifyDisplay(hud, report);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Require(EditorSceneManager.SaveScene(scene), "씬 저장 실패");
                    File.WriteAllText(Output + "/current-only.txt", "PASS 현재 마나만 표시, 씬 저장\n" + report);
                }
                catch (Exception error) { File.WriteAllText(Output + "/current-only.txt", "FAIL " + error); Debug.LogException(error); }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/presentation.request")) return;
            File.Delete(Output + "/presentation.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/presentation.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Player/마나 텍스트 및 스킬 비용 설정")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            var report = new System.Text.StringBuilder();
            foreach (var pair in Costs)
            {
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(
                    "Assets/Nytherion/Data/ScriptableObjects/Skill/" + pair.Key + ".asset");
                Require(skill != null, pair.Key + " 데이터 누락");
                skill.manaCost = pair.Value;
                EditorUtility.SetDirty(skill);
                Require(skill.Description.Contains("[" + pair.Value + "]"), pair.Key + " 설명 비용 불일치");
                report.AppendLine(pair.Key + ": " + pair.Value);
            }
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationTables.UI);
            if (collection != null)
            {
                foreach (var table in collection.StringTables)
                {
                    bool english = table.LocaleIdentifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
                    foreach (string key in new[] { "ui.stat.maxMana", "ui.stat.manaRegenPerSecond", "ui.skill.mana_cost" })
                    {
                        var translation = Localization.LocalizationTranslationCatalog.UIEntries[key];
                        string value = english ? translation.English : translation.Korean;
                        var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
                        entry.Value = value;
                        entry.IsSmart = value.Contains("{0}");
                    }
                    EditorUtility.SetDirty(table);
                }
                EditorUtility.SetDirty(collection.SharedData);
            }
            AssetDatabase.SaveAssets();

            const string scenePath = "Assets/Scenes/GameScene.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) Require(EditorSceneManager.SaveScene(scene,
                    Output + "/GameScene-before-text-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true), "씬 사본 저장 실패");
                HPBarUI hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HPBarUI>(true)).Single();
                var properties = new SerializedObject(hud);
                TextMeshProUGUI hpText = (TextMeshProUGUI)properties.FindProperty("hpText").objectReferenceValue;
                var fill = (UnityEngine.UI.Image)properties.FindProperty("mpFillImage").objectReferenceValue;
                Transform frame = fill.transform.parent;
                TextMeshProUGUI text = frame.Find("MPText")?.GetComponent<TextMeshProUGUI>();
                if (text == null)
                {
                    GameObject child = UnityEngine.Object.Instantiate(hpText.gameObject, frame, false);
                    child.name = "MPText";
                    text = child.GetComponent<TextMeshProUGUI>();
                }
                text.transform.SetAsLastSibling();
                RectTransform rect = text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(8f, 4f);
                rect.offsetMax = new Vector2(-8f, -4f);
                text.fontSize = 22f;
                text.fontSizeMin = 18f;
                text.fontSizeMax = 22f;
                text.enableAutoSizing = true;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.raycastTarget = false;
                text.text = "100";
                properties.FindProperty("mpText").objectReferenceValue = text;
                properties.ApplyModifiedPropertiesWithoutUndo();
                text.ForceMeshUpdate(true);
                Require(text.preferredHeight <= rect.rect.height, "마나 텍스트 높이 초과");
                Require(text.preferredWidth <= rect.rect.width, "마나 텍스트 너비 초과");
                VerifyDisplay(hud, report);
                EditorSceneManager.MarkSceneDirty(scene);
                Require(EditorSceneManager.SaveScene(scene), "씬 저장 실패");
                report.AppendLine("PASS MPText 22 (최소 18), 중앙 정렬, 여백 좌우 8 / 상하 4, 참조 및 텍스트 영역 검증");
                File.WriteAllText(Output + "/presentation.txt", report.ToString());
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void VerifyDisplay(HPBarUI source, System.Text.StringBuilder report)
        {
            GameObject owner = new GameObject("마나 표시 검증");
            GameObject clone = UnityEngine.Object.Instantiate(source.transform.parent.gameObject);
            PlayerData data = ScriptableObject.CreateInstance<PlayerData>();
            try
            {
                PlayerSkillManager mana = owner.AddComponent<PlayerSkillManager>();
                HPBarUI hud = clone.GetComponentInChildren<HPBarUI>(true);
                var properties = new SerializedObject(hud);
                TextMeshProUGUI text = (TextMeshProUGUI)properties.FindProperty("mpText").objectReferenceValue;
                hud.ConstructMana(mana);
                Require(text.text == "100", "초기 마나 표시");
                Require(mana.TrySpendMana(25f) && text.text == "75", "마나 소비 표시 갱신");
                Require(!mana.TrySpendMana(80f) && text.text == "75", "사용 실패 표시 유지");
                mana.InitializeMana(0f, 0f);
                Require(text.text == "0", "마나 0 표시");

                CharacterStatsUI sourceStats = source.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CharacterStatsUI>(true)).Single();
                GameObject statsClone = UnityEngine.Object.Instantiate(sourceStats.gameObject);
                statsClone.transform.SetParent(owner.transform, false);
                PlayerManager player = owner.AddComponent<PlayerManager>();
                player.currentPlayerData = data;
                CharacterStatsUI stats = statsClone.GetComponent<CharacterStatsUI>();
                stats.Construct(player);
                stats.RefreshStatsUI();
                string[] labels = statsClone.GetComponentsInChildren<TextMeshProUGUI>(true).Select(label => label.text).ToArray();
                Require(labels.Contains("최대 마나: 100") && labels.Contains("초당 마나 회복: 5"), "인벤토리 마나 능력치 표시");
                var grid = statsClone.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                Require(grid != null, "능력치 그리드 참조");
                int count = statsClone.transform.childCount;
                int rows = Mathf.CeilToInt((float)count / grid.constraintCount);
                float height = rows * grid.cellSize.y + (rows - 1) * grid.spacing.y + grid.padding.vertical;
                Require(height <= ((RectTransform)statsClone.transform).rect.height + 0.01f, "인벤토리 마지막 행 범위");
                report.AppendLine("PASS 마나 소비/실패/0 텍스트 갱신, 인벤토리 최대 마나/초당 회복 생성 및 그리드 범위");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static void Require(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
        }
    }
}
