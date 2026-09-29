using System.IO;
using System.Text;
using Nytherion.UI.Test;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ChainIgnitionDebugPanelSetup
    {
        private const string Output = "output/chain-ignition";

        static ChainIgnitionDebugPanelSetup()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (File.Exists(Output + "/debug-panel.state.request") && !EditorApplication.isCompiling)
            {
                File.Delete(Output + "/debug-panel.state.request");
                File.WriteAllText(Output + "/debug-panel-state.txt", "playing=" + EditorApplication.isPlaying +
                    " paused=" + EditorApplication.isPaused + " switching=" + EditorApplication.isPlayingOrWillChangePlaymode +
                    " time=" + Time.time + " scale=" + Time.timeScale + " frame=" + Time.frameCount +
                    "\n" + string.Join("\n", System.Array.ConvertAll(Object.FindObjectsOfType<DebugPanelUI>(true),
                        panel => panel.name + " scene=" + panel.gameObject.scene.path)));
            }
            if (File.Exists(Output + "/debug-panel.stop.request") && !EditorApplication.isCompiling)
            {
                File.Delete(Output + "/debug-panel.stop.request");
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/debug-panel.setup.request"))
            {
                File.Delete(Output + "/debug-panel.setup.request");
                Setup();
            }
            if (!File.Exists(Output + "/debug-panel.inspect.request")) return;
            File.Delete(Output + "/debug-panel.inspect.request");
            var report = new StringBuilder();
            foreach (DebugPanelUI panel in Object.FindObjectsOfType<DebugPanelUI>(true))
            {
                report.AppendLine("SCENE " + panel.gameObject.scene.path);
                foreach (RectTransform rect in panel.GetComponentsInChildren<RectTransform>(true))
                {
                    report.AppendLine(GetPath(rect, panel.transform) + " active=" + rect.gameObject.activeSelf +
                        " rect=" + rect.rect + " anchors=" + rect.anchorMin + "/" + rect.anchorMax +
                        " pos=" + rect.anchoredPosition + " size=" + rect.sizeDelta +
                        " components=" + string.Join(",", System.Array.ConvertAll(rect.GetComponents<Component>(), c => c != null ? c.GetType().Name : "Missing")));
                }
            }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/debug-panel-hierarchy.txt", report.ToString());
        }

        [MenuItem("Tools/Nytherion/Chain Ignition/Setup Debug Panel")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            foreach (DebugPanelUI panel in Object.FindObjectsOfType<DebugPanelUI>(true))
            {
                if (panel.gameObject.scene.path != "Assets/Scenes/GameScene.unity") continue;
                Transform content = panel.transform.Find("ContentPanel2");
                if (content == null)
                {
                    File.WriteAllText(Output + "/debug-panel-setup.txt", "ContentPanel2를 찾을 수 없습니다.");
                    return;
                }
                // 저장되지 않은 기존 씬 편집 내용도 복사본으로 보존한 뒤 해당 패널만 확장합니다.
                string backup = Output + "/GameScene-before-debug-panel.unity";
                if (!File.Exists(backup) && !EditorSceneManager.SaveScene(panel.gameObject.scene, backup, true)) return;
                Undo.SetCurrentGroupName("연쇄 점화 디버그 UI 추가");
                ChainIgnitionDebugUI ui = content.GetComponent<ChainIgnitionDebugUI>();
                if (ui == null) ui = Undo.AddComponent<ChainIgnitionDebugUI>(content.gameObject);
                var uiObject = new SerializedObject(ui);
                uiObject.FindProperty("sourceData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ChainIgnitionSkillData>(ChainIgnitionSkillSetup.DataPath);
                uiObject.ApplyModifiedProperties();
                bool needsControls = content.Find("ChainIgnitionControls") == null;
                ui.BuildControls(panel.GetComponentInChildren<TMP_Text>(true));
                if (needsControls) Undo.RegisterCreatedObjectUndo(content.Find("ChainIgnitionControls").gameObject, "연쇄 점화 컨트롤 추가");
                var panelObject = new SerializedObject(panel);
                panelObject.FindProperty("contentPanel2").objectReferenceValue = content.gameObject;
                panelObject.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
                bool saved = EditorSceneManager.SaveScene(panel.gameObject.scene);
                File.WriteAllText(Output + "/debug-panel-setup.txt", "RESULT " + (saved ? "PASS" : "FAIL") +
                    "\nGameScene/DebugPanel/ContentPanel2: UI 생성 및 데이터/패널 참조 연결\n기존 씬 보존 복사본: " + backup);
                return;
            }
        }

        private static string GetPath(Transform current, Transform root)
        {
            if (current == root) return current.name;
            return GetPath(current.parent, root) + "/" + current.name;
        }
    }
}
