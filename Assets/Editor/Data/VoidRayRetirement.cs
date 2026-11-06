using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>공허 광선의 참조와 전용 에셋만 명시적 요청으로 삭제합니다.</summary>
    [InitializeOnLoad]
    public static class VoidRayRetirement
    {
        private const string Output = "output/voidray-retirement";
        private const string ManifestPath = "Documentation/VoidRayRetirementAssets.json";
        private const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/VoidRay.asset";
        private const string DatabasePath = "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset";
        private const string PoolPath = "Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Weapon/Rare_Weapon.asset";

        [Serializable] private class Entry { public string path; public string guid; }
        [Serializable] private class Manifest { public Entry[] assets; }

        static VoidRayRetirement() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/delete.request")) return;
            File.Delete(Output + "/delete.request");
            Apply();
        }

        [MenuItem("Tools/Nytherion/Weapons/Retire Void Ray")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            var report = new List<string>();
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
                Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
                var paths = new HashSet<string>(manifest.assets.Select(entry => entry.path));
                foreach (Entry entry in manifest.assets)
                {
                    if (!entry.path.StartsWith("Assets/", StringComparison.Ordinal) || entry.path.Contains(".."))
                        throw new InvalidOperationException("잘못된 삭제 경로: " + entry.path);
                    if (File.Exists(entry.path) && AssetDatabase.AssetPathToGUID(entry.path) != entry.guid)
                        throw new InvalidOperationException("GUID가 변경된 에셋은 삭제하지 않습니다: " + entry.path);
                }
                // 두 목록 외의 새 참조가 생겼으면 참조를 지우기 전에 중단합니다.
                foreach (string path in AssetDatabase.GetAllAssetPaths())
                {
                    if (!path.StartsWith("Assets/", StringComparison.Ordinal) || paths.Contains(path) ||
                        path == DatabasePath || path == PoolPath || AssetDatabase.IsValidFolder(path)) continue;
                    string dependency = AssetDatabase.GetDependencies(path, false).FirstOrDefault(paths.Contains);
                    if (dependency != null) throw new InvalidOperationException("외부 참조 발견: " + path + " → " + dependency);
                }
                UnityEngine.Object data = AssetDatabase.LoadMainAssetAtPath(DataPath);
                if (data != null)
                {
                    RemoveReference(DatabasePath, "allItems", null, data, report);
                    RemoveReference(PoolPath, "items", "item", data, report);
                }
                // 데이터와 프리팹을 먼저 제거하고 마지막에 스크립트를 삭제합니다.
                foreach (Entry entry in manifest.assets.OrderBy(entry => entry.path.EndsWith(".cs", StringComparison.Ordinal)))
                {
                    if (File.Exists(entry.path) && !AssetDatabase.DeleteAsset(entry.path))
                        throw new InvalidOperationException("삭제 실패: " + entry.path);
                    if (File.Exists(entry.path) || File.Exists(entry.path + ".meta"))
                        throw new InvalidOperationException("파일 또는 meta 잔존: " + entry.path);
                    report.Add("PASS 삭제 " + entry.path);
                }
                report.Add("PASS 전용 에셋 " + manifest.assets.Length + "개 및 meta 삭제, 외부 참조 없음");
            }
            catch (Exception error)
            {
                report.Add("FAIL " + error);
                Debug.LogException(error);
            }
            File.WriteAllLines(Output + "/deletion.txt", report);
        }

        private static void RemoveReference(string path, string property, string child,
            UnityEngine.Object data, List<string> report)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) throw new InvalidOperationException("목록 에셋 누락: " + path);
            var serialized = new SerializedObject(asset);
            SerializedProperty list = serialized.FindProperty(property);
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                SerializedProperty reference = child == null ? element : element.FindPropertyRelative(child);
                if (reference.objectReferenceValue != data) continue;
                int count = list.arraySize;
                list.DeleteArrayElementAtIndex(i);
                if (list.arraySize == count) list.DeleteArrayElementAtIndex(i);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(asset);
            report.Add("PASS 참조 제거 " + path);
        }
    }
}
