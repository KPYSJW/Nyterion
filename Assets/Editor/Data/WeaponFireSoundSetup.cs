using System;
using System.Collections.Generic;
using System.IO;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>게임용 효과음을 Unity 에셋 API로 가져오고 무기 데이터에 연결합니다.</summary>
    [InitializeOnLoad]
    public static class WeaponFireSoundSetup
    {
        public const string Output = "output/weapon-fire-audio";
        public const string RevisionOutput = Output + "/revision";
        public const string AudioFolder = "Assets/Nytherion/Audio/Weapons/";
        public const string DataFolder = "Assets/Nytherion/Data/ScriptableObjects/Weapons/";
        public static readonly string[] Names = {
            "Afterglow", "ArcaneDart", "Blazeshade", "Capricious", "EvilEye", "ForestThorn",
            "Frenzy", "Isolation", "Icicle", "CrimsonLotus", "Obliteration", "Pyrofall",
            "Ignition", "Radiance", "ShamanStaff", "SpiritsGuidance", "Stormbringer",
            "Corrosion", "VoidRay"
        };
        private static readonly float[] Volumes = {
            0.64f, 0.48f, 0.62f, 0.6f, 0.64f, 0.48f, 0.4f, 0.65f, 0.58f,
            0.53f, 0.6f, 0.62f, 0.5f, 0.6f, 0.6f, 0.55f, 0.58f, 0.57f, 0.52f
        };

        static WeaponFireSoundSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            string revisionRequest = RevisionOutput + "/setup.request";
            if (File.Exists(revisionRequest))
            {
                File.Delete(revisionRequest);
                try { ApplyRevision(); }
                catch (Exception error)
                {
                    File.WriteAllText(RevisionOutput + "/setup.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
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

        [MenuItem("Tools/Nytherion/발사 효과음/게임 공격 효과음 갱신")]
        public static void ApplyRevision()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(RevisionOutput);
            var report = new List<string>();
            foreach (string name in Names)
            {
                if (name == "ArcaneDart" || name == "Frenzy" || name == "ForestThorn") continue;
                AudioClip clip = ImportClip(AudioFolder + name + "_Fire.wav");
                WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataFolder + name + ".asset");
                if (clip == null || data == null || data.fireSound != clip)
                    throw new InvalidOperationException("기존 효과음 참조 확인 실패: " + name);
                report.Add($"PASS {name}: 기존 GUID/음량 보존, {clip.length:F3}s");
            }
            ImportClip(AudioFolder + "ShamanStaffAbyssal_Fire.wav");
            ConnectBlazeshadeSummon();
            report.Add("PASS Blazeshade: 일반 모드 피격음, TargetMaker 생성음 별도 연결");
            report.Add("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            File.WriteAllLines(RevisionOutput + "/setup.txt", report);
        }

        private static void ConnectBlazeshadeSummon()
        {
            AudioClip summon = ImportClip(AudioFolder + "Blazeshade_Summon.wav");
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataFolder + "Blazeshade.asset");
            if (summon == null || data == null || data.weaponPrefab == null)
                throw new InvalidOperationException("Blazeshade 소환음/무기 프리팹 누락");
            string path = AssetDatabase.GetAssetPath(data.weaponPrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var settings = new SerializedObject(root.GetComponent<BlazeshadeWeapon>());
                settings.FindProperty("targetSummonSound").objectReferenceValue = summon;
                settings.FindProperty("targetSummonSoundVolume").floatValue = 0.5f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Tools/Nytherion/발사 효과음/무기별 합성 효과음 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            var report = new List<string>();
            for (int i = 0; i < Names.Length; i++)
            {
                string path = AudioFolder + Names[i] + "_Fire.wav";
                AudioClip clip = ImportClip(path);
                WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataFolder + Names[i] + ".asset");
                if (data == null || data.weaponPrefab == null)
                    throw new InvalidOperationException("무기 데이터/프리팹 누락: " + Names[i]);
                // 이미 지정한 사용자 효과음과 볼륨은 재실행 시 보존합니다.
                if (data.fireSound == null)
                {
                    data.fireSound = clip;
                    data.fireSoundVolume = Volumes[i];
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssetIfDirty(data);
                }
                report.Add($"PASS {Names[i]}: {data.fireSound.name}, {clip.length:F3}s, gain={data.fireSoundVolume:F2}");
            }

            AudioClip abyss = ImportClip(AudioFolder + "ShamanStaffAbyssal_Fire.wav");
            WeaponData shaman = AssetDatabase.LoadAssetAtPath<WeaponData>(DataFolder + "ShamanStaff.asset");
            string prefabPath = AssetDatabase.GetAssetPath(shaman.weaponPrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var settings = new SerializedObject(root.GetComponent<ShamanStaff>());
                SerializedProperty sound = settings.FindProperty("abyssalFlameFireSound");
                if (sound.objectReferenceValue == null)
                {
                    sound.objectReferenceValue = abyss;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            report.Add("PASS 주술사의 지팡이 심연의 불꽃 전용 사운드 연결");
            ConnectBlazeshadeSummon();
            report.Add("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            File.WriteAllLines(Output + "/setup.txt", report);
            Debug.Log("[WeaponFireSoundSetup] 19종 무기와 심연의 불꽃 발사 효과음 연결 완료.");
        }

        private static AudioClip ImportClip(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("발사 효과음 누락: " + path);
            importer.forceToMono = true;
            importer.loadInBackground = false;
            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            sample.preloadAudioData = true;
            sample.loadType = AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = AudioCompressionFormat.PCM;
            sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = sample;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
