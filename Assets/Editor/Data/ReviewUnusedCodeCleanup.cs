using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Nytherion.Editor
{
    /// <summary>리뷰에서 확인한 후보만 참조를 재검사하고 원본을 보관한 뒤 Unity API로 제거한다.</summary>
    [InitializeOnLoad]
    public static class ReviewUnusedCodeCleanup
    {
        private const string Output = "output/review-unused-code";
        private static readonly string[] Paths =
        {
            "Assets/Nytherion/Core/Systems/VContainerBootstrap.cs",
            "Assets/Nytherion/UI/Title/SimpleStartButton.cs",
            "Assets/Nytherion/UI/Title/StartButtonHandler.cs",
            "Assets/Nytherion/Core/Interfaces/ICurrencySaveService.cs",
            "Assets/Nytherion/Core/Interfaces/IInventorySaveService.cs",
            "Assets/Nytherion/Core/Interfaces/IRelicSaveService.cs"
        };

        static ReviewUnusedCodeCleanup() => EditorApplication.update += Update;
        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/cleanup.request")) return;
            File.Delete(Output + "/cleanup.request");
            var results = new List<string>();
            try
            {
                string[] remaining = Paths.Where(File.Exists).ToArray();
                if (remaining.Length == 0) { results.Add("PASS 대상 6개가 이미 제거되어 추가 변경 없음"); return; }
                foreach (string path in remaining)
                {
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    string typeName = Path.GetFileNameWithoutExtension(path);
                    if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("GUID 누락: " + path);
                    foreach (string other in AssetDatabase.GetAllAssetPaths())
                    {
                        if (!File.Exists(other) || other == path || other.EndsWith(".meta")) continue;
                        string extension = Path.GetExtension(other).ToLowerInvariant();
                        if (extension == ".cs")
                        {
                            if (other.EndsWith("/ReviewUnusedCodeCleanup.cs")) continue;
                            if (Regex.IsMatch(File.ReadAllText(other), @"\b" + typeName + @"\b"))
                                throw new InvalidOperationException(typeName + " 코드 참조: " + other);
                        }
                        else if (new[] { ".prefab", ".unity", ".asset", ".controller", ".overridecontroller", ".playable", ".anim", ".json" }.Contains(extension) &&
                            File.ReadAllText(other).Contains(guid))
                            throw new InvalidOperationException(typeName + " 에셋 참조: " + other);
                    }
                    results.Add("PASS 코드·직렬화 참조 없음: " + typeName);
                }
                Directory.CreateDirectory("Documentation");
                const string archive = "Documentation/ReviewStage6Originals.zip";
                if (File.Exists(archive)) throw new InvalidOperationException("기존 원본 보관 파일을 덮어쓰지 않습니다: " + archive);
                using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
                    foreach (string path in remaining)
                    {
                        zip.CreateEntryFromFile(path, path);
                        zip.CreateEntryFromFile(path + ".meta", path + ".meta");
                    }
                foreach (string path in remaining)
                {
                    if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("삭제 실패: " + path);
                    results.Add("PASS 원본·meta 보관 후 AssetDatabase.DeleteAsset: " + path);
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally { File.WriteAllLines(Output + "/verification.txt", results); }
        }
    }
}
