using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class MuzzleFlashSetup
    {
        public const string Output = "output/muzzle-flash";
        public const string RapidPath = "Assets/Prefabs/Gameplay/Combat/VFX/FrenzyMuzzleFlash.prefab";
        public const string ScenePath = "Assets/Scenes/MuzzleFlashTest.unity";
        static readonly string[] retired =
        {
            "Assets/Prefabs/Gameplay/Combat/VFX/SmallMuzzleFlash.prefab",
            "Assets/Nytherion/Gameplay/Combat/VFX/MuzzleFlashEffect.cs",
            "Assets/Nytherion/Art/Common/Materials/MuzzleSpark.mat",
            "Assets/Nytherion/Art/Common/Shaders/MuzzleSpark.shader"
        };

        static MuzzleFlashSetup() => EditorApplication.update += Update;

        static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            string verification = Output + "/verify-authoring.request";
            if (File.Exists(verification))
            {
                File.Delete(verification);
                WeaponFireEffectVerification.Run();
                return;
            }
            string request = Output + "/cleanup-particles.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try
            {
                CleanupParticles();
                File.WriteAllText(Output + "/cleanup.txt", "PASS 기본 파티클·전용 코드·재질·셰이더 정리 / 테스트 씬 참조 제거\n");
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/cleanup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        public static void WriteDependencies()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/dependencies.txt", AssetDatabase.GetDependencies(
                new[] { "Assets/Nytherion/Data/ScriptableObjects/Weapons/Frenzy.asset", RapidPath, ScenePath }, true));
        }

        static void CleanupParticles()
        {
            if (File.Exists(ScenePath))
            {
                Scene previous = SceneManager.GetActiveScene();
                Scene loaded = SceneManager.GetSceneByPath(ScenePath);
                bool opened = !loaded.IsValid() || !loaded.isLoaded;
                Scene scene = opened ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : loaded;
                try
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
            }
            string[] extensions = { ".prefab", ".unity", ".asset", ".mat", ".anim", ".controller" };
            foreach (string path in retired)
            {
                if (!File.Exists(path)) continue;
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("에셋 GUID 누락: " + path);
                foreach (string file in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories))
                {
                    string normalized = file.Replace('\\', '/');
                    if (retired.Contains(normalized) || !extensions.Contains(Path.GetExtension(file))) continue;
                    if (File.ReadAllText(file).Contains(guid))
                        throw new InvalidOperationException("기본 파티클을 참조하는 에셋이 남아 있습니다: " + normalized);
                }
            }
            foreach (string path in retired)
            {
                if (!File.Exists(path)) continue;
                string backup = Output + "/retired-particles/" + path;
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(path, backup, true);
                if (File.Exists(path + ".meta")) File.Copy(path + ".meta", backup + ".meta", true);
                if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("에셋 정리 실패: " + path);
            }
            WriteDependencies();
        }

        [MenuItem("Tools/Nytherion/발사 연출/Frenzy 테스트 씬 열기")]
        public static void OpenTestScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}

