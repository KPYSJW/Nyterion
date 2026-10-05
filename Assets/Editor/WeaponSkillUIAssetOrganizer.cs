using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>정리 목록에 있는 에셋만 GUID를 보존하며 이동합니다.</summary>
    [InitializeOnLoad]
    public static class WeaponSkillUIAssetOrganizer
    {
        private const string PlanPath = "Documentation/WeaponSkillUIAssetMoves.json";
        private const string Output = "output/asset-organization";

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

        static WeaponSkillUIAssetOrganizer() => EditorApplication.update += Update;

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
                File.WriteAllText(Output + "/result.txt", "FAIL\n" + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Assets/Organize Weapon Skill UI")]
        public static void Organize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            MovePlan plan = JsonUtility.FromJson<MovePlan>(File.ReadAllText(PlanPath));
            if (plan == null || plan.entries == null || plan.entries.Length == 0)
                throw new InvalidOperationException("에셋 정리 목록이 비어 있습니다.");

            // 전체 대상을 먼저 확인합니다. 완료된 이동은 다시 실행해도 건너뜁니다.
            foreach (MoveEntry entry in plan.entries)
            {
                string current = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (!string.Equals(current, entry.source, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(current, entry.destination, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("원본 GUID 또는 경로 불일치: " + entry.source);
                if (current == entry.source && (File.Exists(entry.destination) ||
                    !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(entry.destination))))
                    throw new InvalidOperationException("대상 경로가 이미 사용 중입니다: " + entry.destination);
            }

            int moved = 0;
            foreach (MoveEntry entry in plan.entries)
            {
                if (string.Equals(AssetDatabase.GUIDToAssetPath(entry.guid), entry.destination,
                    StringComparison.OrdinalIgnoreCase)) continue;
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
                    throw new InvalidOperationException("이동 후 에셋 로드 실패: " + entry.destination);
            }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/result.txt", "PASS\n검증: " + plan.entries.Length + "개\n이번 이동: " + moved + "개");
            Debug.Log("[AssetOrganizer] 무기·스킬·UI 에셋 정리 및 GUID 검증 완료.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("폴더 생성 실패: " + path);
        }

    }
}
