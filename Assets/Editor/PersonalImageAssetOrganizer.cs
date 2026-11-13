using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>작성 이력 또는 사용자 확인으로 분류한 본인 이미지만 GUID를 보존하며 정리합니다.</summary>
    [InitializeOnLoad]
    public static class PersonalImageAssetOrganizer
    {
        private const string PlanPath = "Documentation/PersonalImageAssetMoves.json";
        private const string Output = "output/image-organization";

        [Serializable]
        private class MoveEntry
        {
            public string source;
            public string destination;
            public string guid;
        }

        [Serializable]
        private class MovePlan
        {
            public MoveEntry[] entries;
        }

        static PersonalImageAssetOrganizer() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = Output + "/move.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Organize(); }
            catch (Exception error)
            {
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/result.txt", "FAIL\n" + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Assets/Organize Personal Images")]
        public static void Organize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("컴파일과 가져오기가 끝난 편집 모드에서 실행해 주세요.");

            MovePlan plan = JsonUtility.FromJson<MovePlan>(File.ReadAllText(PlanPath));
            if (plan == null || plan.entries == null || plan.entries.Length == 0)
                throw new InvalidOperationException("이미지 정리 목록이 비어 있습니다.");

            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // 전체 경로와 GUID를 검증한 뒤 이동합니다. 이미 완료된 항목은 건너뜁니다.
            foreach (MoveEntry entry in plan.entries)
            {
                ValidatePath(entry.source);
                ValidatePath(entry.destination);
                if (string.IsNullOrEmpty(entry.guid) || !guids.Add(entry.guid) ||
                    !destinations.Add(entry.destination))
                    throw new InvalidOperationException("중복되거나 비어 있는 GUID 또는 대상 경로: " + entry.source);
                string current = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (!string.Equals(current, entry.source, StringComparison.Ordinal) &&
                    !string.Equals(current, entry.destination, StringComparison.Ordinal))
                    throw new InvalidOperationException("원본 GUID 또는 경로 불일치: " + entry.source);
                if (current == entry.source && (File.Exists(entry.destination) ||
                    Directory.Exists(entry.destination) || File.Exists(entry.destination + ".meta") ||
                    !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(entry.destination))))
                    throw new InvalidOperationException("대상 경로가 이미 사용 중입니다: " + entry.destination);
            }

            int moved = 0;
            foreach (MoveEntry entry in plan.entries)
            {
                if (AssetDatabase.GUIDToAssetPath(entry.guid) == entry.destination) continue;
                EnsureFolder(Path.GetDirectoryName(entry.destination).Replace('\\', '/'));
                string error = AssetDatabase.MoveAsset(entry.source, entry.destination);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                moved++;
            }
            AssetDatabase.Refresh();

            foreach (MoveEntry entry in plan.entries)
            {
                if (AssetDatabase.AssetPathToGUID(entry.destination) != entry.guid ||
                    AssetDatabase.LoadMainAssetAtPath(entry.destination) == null)
                    throw new InvalidOperationException("이동 후 이미지 로드 실패: " + entry.destination);
            }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/result.txt",
                "PASS\n검증: " + plan.entries.Length + "개\n이번 이동: " + moved + "개");
            Debug.Log("[PersonalImageAssetOrganizer] 이미지 " + plan.entries.Length + "개 정리 및 GUID 검증 완료.");
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("..") || path.Contains("\\") || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("허용되지 않은 에셋 경로: " + path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'),
                    Path.GetFileName(path))))
                throw new InvalidOperationException("폴더 생성 실패: " + path);
        }
    }
}
