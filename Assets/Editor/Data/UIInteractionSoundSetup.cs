using System;
using System.Collections.Generic;
using System.IO;
using Nytherion.Core.Managers;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    /// <summary>ElevenLabs UI 조작음을 Unity 임포터와 오디오 매니저 프리팹에 연결합니다.</summary>
    [InitializeOnLoad]
    public static class UIInteractionSoundSetup
    {
        public const string Output = "output/ui-audio";
        public const string Folder = "Assets/Nytherion/Audio/UI/";
        public const string AudioPrefab = "Assets/Prefabs/Infrastructure/Managers/AudioManager.prefab";
        public const string PanelCloseClip = Folder + "UI_PanelClose_Leather.wav";
        public static readonly string[] Names = { "Hover", "Click", "Close", "Adjust", "Pickup", "Place", "Success", "Error" };

        static UIInteractionSoundSetup() => EditorApplication.update += Update;

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

        [MenuItem("Tools/Nytherion/UI 조작 효과음/효과음 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            ApplyPanelCloseSound();
            var clips = new List<AudioClip>();
            var report = new List<string>();
            foreach (string name in Names)
            {
                string path = Folder + "UI_" + name + ".wav";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("효과음 누락: " + path);
                importer.forceToMono = name != "Close";
                importer.loadInBackground = false;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.preloadAudioData = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || clip.length > 0.65f) throw new InvalidOperationException("짧은 UI 클립 임포트 실패: " + path);
                clips.Add(clip);
                report.Add($"PASS {name}: {clip.length:F3}s, {clip.frequency} Hz, {clip.channels}ch");
            }
            GameObject root = PrefabUtility.LoadPrefabContents(AudioPrefab);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<AudioManager>());
                for (int i = 0; i < Names.Length; i++)
                    serialized.FindProperty(GetSoundProperty(Names[i])).objectReferenceValue = clips[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, AudioPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var saved = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(AudioPrefab).GetComponent<AudioManager>());
            for (int i = 0; i < Names.Length; i++)
                if (saved.FindProperty(GetSoundProperty(Names[i])).objectReferenceValue != clips[i])
                    throw new InvalidOperationException("프리팹 참조 누락: " + Names[i]);
            report.Add("PASS AudioManager 프리팹 8종 참조; 기존 창 열기/BGM 참조 보존");
            report.Add("Scene: " + SceneManager.GetActiveScene().path + ", dirty=" + SceneManager.GetActiveScene().isDirty);
            File.WriteAllLines(Output + "/setup.txt", report);
        }

        // 기존 오디오 에셋 경로는 유지하고 상점 전용 필드에 연결합니다.
        internal static string GetSoundProperty(string name) =>
            name == "Success" ? "uiShopTransactionSound" : "ui" + name + "Sound";

        private static void ApplyPanelCloseSound()
        {
            AssetDatabase.ImportAsset(PanelCloseClip, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(PanelCloseClip) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("패널 닫기 효과음 누락: " + PanelCloseClip);
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(PanelCloseClip);
            foreach (string path in UIOpenSoundSetup.PanelPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var serialized = new SerializedObject(root.GetComponent<UIPanelBase>());
                    serialized.FindProperty("closeSoundOverride").objectReferenceValue = clip;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var report = new List<string> { $"PASS 첨부 패널 닫기 효과음: {clip.name}, {clip.length:F3}s" };
            foreach (string path in UIOpenSoundSetup.PanelPaths)
            {
                var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<UIPanelBase>());
                if (serialized.FindProperty("closeSoundOverride").objectReferenceValue != clip)
                    throw new InvalidOperationException("패널 닫기 효과음 참조 누락: " + path);
                report.Add("PASS " + path);
            }
            File.WriteAllLines(Output + "/panel-close-setup.txt", report);
        }
    }
}
