using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Nytherion.Data.ScriptableObjects.Relics;

namespace Nytherion.Editor
{
    /// <summary>푸른 슬라임 에셋의 GUID와 저장 ID를 보존하며 이름을 정리합니다.</summary>
    [InitializeOnLoad]
    public static class BlueSlimeAssetSetup
    {
        static BlueSlimeAssetSetup() => EditorApplication.update += CheckRequest;

        private static void CheckRequest()
        {
            const string request = "output/blue-slime/apply.request";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            File.Delete(request);
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText("output/blue-slime/result.txt", "FAIL: " + error);
                Debug.LogException(error);
            }
        }

        public static void Apply()
        {
            const string previous = "Pengoo";
            string[] paths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/") && Path.GetFileName(path).Contains(previous))
                .OrderByDescending(path => path.Length).ToArray();
            foreach (string path in paths)
            {
                string destination = path.Replace(previous, "BlueSlime");
                // 부모 폴더는 마지막에 이동하므로 현재 부모 경로를 유지합니다.
                destination = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + Path.GetFileName(destination);
                if (File.Exists(destination) || Directory.Exists(destination))
                    throw new InvalidOperationException("이름 변경 대상이 이미 존재합니다: " + destination);
            }
            foreach (string path in paths)
            {
                string destination = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + Path.GetFileName(path).Replace(previous, "BlueSlime");
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (path.EndsWith(".prefab"))
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        root.name = root.name.Replace(previous, "BlueSlime");
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                else if (path.EndsWith(".anim") || path.EndsWith(".controller") || path.EndsWith(".asset"))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    asset.name = asset.name.Replace(previous, "BlueSlime");
                    EditorUtility.SetDirty(asset);
                }
                string error = AssetDatabase.MoveAsset(path, destination);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                if (guid != AssetDatabase.AssetPathToGUID(destination))
                    throw new InvalidOperationException("GUID가 변경되었습니다: " + destination);
            }
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") && path.EndsWith(".cs") && !path.EndsWith("/BlueSlimeAssetSetup.cs")))
            {
                string source = File.ReadAllText(path);
                string updated = source.Replace(previous, "BlueSlime").Replace("펭구", "푸른 슬라임");
                if (source != updated) File.WriteAllText(path, updated);
            }
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(
                "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/BlueSlime.asset");
            if (relic == null || relic.relicName != "Frog")
                throw new InvalidOperationException("푸른 슬라임 유물의 기존 저장 ID를 확인할 수 없습니다.");
            var collection = LocalizationEditorSettings.GetStringTableCollection("Relics");
            RelicData portal = AssetDatabase.LoadAssetAtPath<RelicData>(
                "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Relic_PortalGate.asset");
            if (collection != null)
            {
                foreach (StringTable table in collection.StringTables)
                {
                    bool korean = table.LocaleIdentifier.Code.StartsWith("ko", StringComparison.OrdinalIgnoreCase);
                    bool english = table.LocaleIdentifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
                    if (!korean && !english) continue;
                    table.AddEntry("relic.Frog.name", korean ? relic.koreanName : relic.EnglishName);
                    table.AddEntry("relic.Frog.description", korean ? relic.description_KR : relic.description_EN);
                    table.AddEntry("relic.Relic of Portal Gate.name", korean ? portal.koreanName : portal.EnglishName);
                    EditorUtility.SetDirty(table);
                }
                EditorUtility.SetDirty(collection.SharedData);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Directory.CreateDirectory("output/blue-slime");
            File.WriteAllText("output/blue-slime/result.txt", "PASS: " + paths.Length + " assets moved with GUIDs preserved; Frog save ID verified.");
        }
    }
}
