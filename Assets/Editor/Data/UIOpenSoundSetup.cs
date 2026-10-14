using System;
using System.Collections.Generic;
using System.IO;
using Nytherion.Core.Managers;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>공통 UI 열기 효과음을 가져와 오디오 매니저와 대상 패널 프리팹에 연결한다.</summary>
    [InitializeOnLoad]
    public static class UIOpenSoundSetup
    {
        public const string Output = "output/ui-open-audio";
        public const string ClipPath = "Assets/Nytherion/Audio/UI/turn_page.wav";
        public const string AudioPrefabPath = "Assets/Prefabs/Infrastructure/Managers/AudioManager.prefab";
        public static readonly string[] PanelPaths =
        {
            "Assets/Prefabs/UI/Inventory/InventoryUI.prefab",
            "Assets/Prefabs/Infrastructure/Managers/RelicUIController.prefab",
            "Assets/Prefabs/Infrastructure/Managers/SkillUIController.prefab"
        };

        static UIOpenSoundSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = Output + "/setup.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/UI 열기 효과음/효과음 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("UI 효과음 파일을 찾을 수 없습니다.");
            importer.loadInBackground = false;
            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            sample.preloadAudioData = true;
            sample.loadType = AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = sample;
            importer.SaveAndReimport();
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null) throw new InvalidOperationException("UI 효과음 임포트 실패");

            EditPrefab(AudioPrefabPath, root =>
            {
                var settings = new SerializedObject(root.GetComponent<AudioManager>());
                settings.FindProperty("uiOpenSound").objectReferenceValue = clip;
                settings.ApplyModifiedPropertiesWithoutUndo();
            });
            foreach (string path in PanelPaths)
            {
                EditPrefab(path, root =>
                {
                    var settings = new SerializedObject(root.GetComponent<UIPanelBase>());
                    settings.FindProperty("playOpenSound").boolValue = true;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                });
            }

            var report = new List<string> { $"PASS 효과음: {clip.name}, {clip.length:F3}s" };
            foreach (string path in PanelPaths)
            {
                var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<UIPanelBase>());
                if (!settings.FindProperty("playOpenSound").boolValue)
                    throw new InvalidOperationException("열기 효과음 설정 누락: " + path);
                report.Add("PASS 프리팹 연결: " + path);
            }
            var audioSettings = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(AudioPrefabPath).GetComponent<AudioManager>());
            if (audioSettings.FindProperty("uiOpenSound").objectReferenceValue != clip)
                throw new InvalidOperationException("오디오 매니저 효과음 참조 누락");
            report.Add("PASS 오디오 매니저 클립 참조, 음량=" + audioSettings.FindProperty("uiOpenSoundVolume").floatValue);
            report.Add("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            report.Add("Dirty: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);
            File.WriteAllLines(Output + "/setup.txt", report);
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
