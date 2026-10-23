using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>명시된 변경 목록을 Unity 에셋 API로 적용하고 GUID·로컬 ID·참조를 검증합니다.</summary>
    [InitializeOnLoad]
    public static class WeaponFileRename
    {
        private const string Output = "output/weapon-presentation";
        [Serializable] private class Replacement { public string old; public string @new; }
        [Serializable] private class Move { public string source; public string destination; public string guid; public bool folder; }
        [Serializable] private class Plan { public Replacement[] mapping; public Move[] moves; public string[] sources; public string[] serialized; }
        [Serializable] private class Snapshot { public string guid; public long[] localIds; public string[] references; public string identity; }
        [Serializable] private class Baseline { public Snapshot[] snapshots; }
        private static Plan plan;
        private static Regex pattern;
        private static Dictionary<string, string> replacements;

        static WeaponFileRename() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = Output + "/rename.request";
            if (File.Exists(request))
            {
                File.Delete(request);
                try { Rename(); }
                catch (Exception error) { File.AppendAllText(Output + "/rename.txt", "FAIL " + error + "\n"); Debug.LogException(error); }
                return;
            }
            request = Output + "/resume-swap.request";
            if (File.Exists(request))
            {
                File.Delete(request);
                try { ResumeSwap(); }
                catch (Exception error) { File.AppendAllText(Output + "/rename.txt", "FAIL " + error + "\n"); Debug.LogException(error); }
                return;
            }
            request = Output + "/reimport-rename.request";
            if (File.Exists(request))
            {
                File.Delete(request);
                LoadPlan();
                foreach (Move move in plan.moves.Where(move => move.destination.EndsWith(".cs", StringComparison.Ordinal)))
                    AssetDatabase.ImportAsset(move.destination, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                foreach (Move move in plan.moves.Where(move => !move.folder && !move.destination.EndsWith(".cs", StringComparison.Ordinal)))
                    AssetDatabase.ImportAsset(move.destination, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                File.WriteAllText(Output + "/reimport-rename.txt", "PASS 스크립트 및 이름 변경 에셋 재임포트\n");
                return;
            }
            request = Output + "/sync-rename.request";
            if (File.Exists(request))
            {
                File.Delete(request);
                LoadPlan();
                foreach (Move move in plan.moves.Where(move => move.destination.EndsWith(".prefab", StringComparison.Ordinal)))
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(move.destination);
                    try
                    {
                        string expectedName = Path.GetFileNameWithoutExtension(move.destination);
                        bool changed = root.name != expectedName;
                        var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
                        var modifications = PrefabUtility.GetPropertyModifications(root);
                        if (source != null && modifications != null)
                        {
                            foreach (var modification in modifications)
                                if (modification.target == source && modification.propertyPath == "m_Name" && modification.value != expectedName)
                                {
                                    modification.value = expectedName;
                                    changed = true;
                                }
                            if (changed) PrefabUtility.SetPropertyModifications(root, modifications);
                        }
                        if (!changed) continue;
                        root.name = expectedName;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
                        PrefabUtility.SaveAsPrefabAsset(root, move.destination);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                File.WriteAllText(Output + "/sync-rename.txt", "PASS 프리팹 내부 이름 동기화\n");
                return;
            }
            request = Output + "/validate-rename.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Validate(); }
            catch (Exception error) { File.AppendAllText(Output + "/rename-validation.txt", "FAIL " + error + "\n"); Debug.LogException(error); }
        }

        private static void LoadPlan()
        {
            plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Output + "/rename-plan.json"));
            replacements = plan.mapping.ToDictionary(item => item.old, item => item.@new);
            pattern = new Regex(string.Join("|", plan.mapping.Select(item => Regex.Escape(item.old))));
        }

        private static string Replace(string value) => pattern.Replace(value, match => replacements[match.Value]);

        private static void Rename()
        {
            LoadPlan();
            File.WriteAllText(Output + "/rename.txt", "RUNNING Unity 에셋 이름 변경\n");
            foreach (Move move in plan.moves)
            {
                Require(AssetDatabase.AssetPathToGUID(move.source) == move.guid, "변경 전 GUID 불일치: " + move.source);
                Require((!File.Exists(move.destination) && !Directory.Exists(move.destination)) ||
                    plan.moves.Any(other => other.source == move.destination), "이미 존재하는 대상: " + move.destination);
            }
            var baseline = new Baseline { snapshots = plan.moves.Where(move => !move.folder).Select(move => Capture(move.guid)).ToArray() };
            File.WriteAllText(Output + "/rename-baseline.json", JsonUtility.ToJson(baseline, true));

            foreach (string path in plan.serialized)
            {
                if (path.EndsWith(".prefab", StringComparison.Ordinal))
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        {
                            EditStrings(child.gameObject);
                            foreach (Component component in child.GetComponents<Component>())
                                if (component != null) EditStrings(component);
                        }
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                else
                {
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (asset != null && !(asset is Sprite))
                        {
                            EditStrings(asset);
                            AssetDatabase.SaveAssetIfDirty(asset);
                        }
                }
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                var renamedFiles = plan.moves.Where(move => !move.folder && Path.GetFileName(move.source) != Replace(Path.GetFileName(move.source))).ToArray();
                foreach (Move move in renamedFiles)
                {
                    string error = AssetDatabase.RenameAsset(move.source, "__NytherionSwap_" + move.guid);
                    Require(string.IsNullOrEmpty(error), error);
                }
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.StartAssetEditing();
                foreach (Move move in renamedFiles)
                {
                    string temporary = Path.GetDirectoryName(move.source).Replace('\\', '/') + "/__NytherionSwap_" + move.guid + Path.GetExtension(move.source);
                    string error = AssetDatabase.RenameAsset(temporary, Path.GetFileNameWithoutExtension(Replace(Path.GetFileName(move.source))));
                    Require(string.IsNullOrEmpty(error), error);
                    File.AppendAllText(Output + "/rename.txt", move.source + " -> " + move.destination + "\n");
                }
                foreach (var depth in plan.moves.Where(move => move.folder).GroupBy(move => move.source.Count(c => c == '/')).OrderByDescending(group => group.Key))
                {
                    foreach (Move move in depth)
                    {
                        string error = AssetDatabase.RenameAsset(move.source, "__NytherionSwap_" + move.guid);
                        Require(string.IsNullOrEmpty(error), error);
                    }
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    AssetDatabase.StartAssetEditing();
                    foreach (Move move in depth)
                    {
                        string temporary = Path.GetDirectoryName(move.source).Replace('\\', '/') + "/__NytherionSwap_" + move.guid;
                        string error = AssetDatabase.RenameAsset(temporary, Replace(Path.GetFileName(move.source)));
                        Require(string.IsNullOrEmpty(error), error);
                        File.AppendAllText(Output + "/rename.txt", move.source + " -> " + move.destination + "\n");
                    }
                }
                // ASCII 식별자만 치환하여 원본 파일의 인코딩·개행·BOM을 보존합니다.
                Encoding byteEncoding = Encoding.GetEncoding(28591);
                foreach (string source in plan.sources)
                {
                    string path = Replace(source);
                    string content = byteEncoding.GetString(File.ReadAllBytes(path));
                    File.WriteAllBytes(path, byteEncoding.GetBytes(Replace(content)));
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            File.AppendAllText(Output + "/rename.txt", $"PASS {plan.moves.Count(move => !move.folder)}개 파일, {plan.moves.Count(move => move.folder)}개 폴더, {plan.sources.Length}개 코드 참조 변경\n");
        }

        private static void ResumeSwap()
        {
            LoadPlan();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (Move move in plan.moves.Where(move => !move.folder))
            {
                string current = AssetDatabase.GUIDToAssetPath(move.guid);
                string expectedName = Path.GetFileNameWithoutExtension(move.destination);
                if (Path.GetFileNameWithoutExtension(current) == expectedName) continue;
                string error = AssetDatabase.RenameAsset(current, expectedName);
                Require(string.IsNullOrEmpty(error), current + ": " + error);
                File.AppendAllText(Output + "/rename.txt", current + " -> " + move.destination + "\n");
            }
            foreach (Move move in plan.moves.Where(move => move.folder).OrderByDescending(move => move.source.Count(c => c == '/')))
            {
                string current = AssetDatabase.GUIDToAssetPath(move.guid);
                if (current == move.destination) continue;
                string error = AssetDatabase.RenameAsset(current, Path.GetFileName(move.destination));
                Require(string.IsNullOrEmpty(error), error);
            }
            Encoding byteEncoding = Encoding.GetEncoding(28591);
            foreach (string source in plan.sources)
            {
                string path = Replace(source);
                string content = byteEncoding.GetString(File.ReadAllBytes(path));
                File.WriteAllBytes(path, byteEncoding.GetBytes(Replace(content)));
            }
            AssetDatabase.Refresh();
            File.AppendAllText(Output + "/rename.txt", $"PASS {plan.moves.Count(move => !move.folder)}개 파일, {plan.moves.Count(move => move.folder)}개 폴더, {plan.sources.Length}개 코드 참조 교환 완료\n");
        }

        private static void EditStrings(Object target)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.String || property.name == "uniqueID") continue;
                string value = property.stringValue;
                string updated = Replace(value);
                if (value == updated) continue;
                property.stringValue = updated;
                changed = true;
            }
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
        }

        private static Snapshot Capture(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var objects = new HashSet<Object>(AssetDatabase.LoadAllAssetsAtPath(path).Where(asset => asset != null));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    objects.Add(child.gameObject);
                    foreach (Component component in child.GetComponents<Component>())
                    {
                        Require(component != null, "누락된 스크립트: " + path);
                        objects.Add(component);
                    }
                }
            var ids = new List<long>();
            var references = new List<string>();
            string identity = string.Empty;
            foreach (Object asset in objects)
            {
                string objectGuid;
                long objectId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out objectGuid, out objectId)) continue;
                ids.Add(objectId);
                if (asset is MonoScript) continue;
                var serialized = new SerializedObject(asset);
                var id = serialized.FindProperty("uniqueID");
                if (id != null) identity = id.stringValue;
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;
                    string referenceGuid;
                    long referenceId;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(property.objectReferenceValue, out referenceGuid, out referenceId))
                        references.Add(objectId + ":" + property.propertyPath + ":" + referenceGuid + ":" + referenceId);
                }
            }
            return new Snapshot { guid = guid, localIds = ids.OrderBy(id => id).ToArray(), references = references.OrderBy(value => value, StringComparer.Ordinal).ToArray(), identity = identity };
        }

        private static void Validate()
        {
            LoadPlan();
            File.WriteAllText(Output + "/rename-validation.txt", "RUNNING 이름 변경 후 참조 검증\n");
            Baseline baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(Output + "/rename-baseline.json"));
            foreach (Move move in plan.moves)
            {
                Require(AssetDatabase.GUIDToAssetPath(move.guid) == move.destination, "변경 후 경로/GUID 불일치: " + move.destination);
                Require((!File.Exists(move.source) && !Directory.Exists(move.source)) ||
                    plan.moves.Any(other => other.destination == move.source), "기존 경로가 남았습니다: " + move.source);
                if (move.folder) continue;
                Snapshot expected = baseline.snapshots.Single(item => item.guid == move.guid);
                Snapshot actual = Capture(move.guid);
                Require(expected.localIds.SequenceEqual(actual.localIds), "로컬 ID 변경: " + move.destination);
                // Sprite의 Atlas 렌더 캐시는 플레이 모드에서 바뀌므로 원본 에셋 참조 비교에서 제외합니다.
                Func<string, bool> sourceReference = value => !value.Contains(":m_AtlasRD.");
                if (!expected.references.Where(sourceReference).SequenceEqual(actual.references.Where(sourceReference)))
                {
                    File.WriteAllText(Output + "/rename-reference-expected.json", JsonUtility.ToJson(expected, true));
                    File.WriteAllText(Output + "/rename-reference-actual.json", JsonUtility.ToJson(actual, true));
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(move.destination);
                    if (prefab != null)
                        File.WriteAllLines(Output + "/rename-script-diagnostic.txt", prefab.GetComponentsInChildren<MonoBehaviour>(true)
                            .Where(component => component != null).Select(component => component.GetType().FullName + " / " +
                                MonoScript.FromMonoBehaviour(component).GetClass()?.FullName));
                    throw new InvalidOperationException("에셋 참조 변경: " + move.destination);
                }
                Require(expected.identity == actual.identity, "저장 데이터 ID 변경: " + move.destination);
                if (move.destination.EndsWith(".cs", StringComparison.Ordinal))
                    Require(AssetDatabase.LoadAssetAtPath<MonoScript>(move.destination).GetClass() != null, "스크립트 클래스 연결 누락: " + move.destination);
            }
            File.AppendAllText(Output + "/rename-validation.txt", $"PASS {plan.moves.Length}개 경로의 GUID, 로컬 ID, 에셋 참조, 저장 ID 및 스크립트 클래스 연결 유지\n");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
