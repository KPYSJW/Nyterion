using System;
using System.IO;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Relics;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Nytherion.Editor
{
    /// <summary>GUID를 보존하며 유물 표시 이름 및 파일명을 변경합니다.</summary>
    [InitializeOnLoad]
    public static class RelicNamingSetup
    {
        private const string Output = "output/relic-naming";

        static RelicNamingSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/apply.request")) return;
            File.Delete(Output + "/apply.request");
            Apply();
        }

        [MenuItem("Tools/Nytherion/Relics/Update Relic Names")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            try
            {
                RenameRelic("9751e2760ade8e4cae771239b9c1d0ef", "OminousMask", "불길한 가면", "Ominous Mask");
                RenameRelic("8951e2760ade8e4cae771239b9c1d0ef", "Capacitor", "축전기", "Capacitor");
                RenameRelic("c11e7a5c8dbfe61438756d24e8480998", "ManaCharger", "마력 충전기", "Mana Charger");
                RenameAsset("86d9103ea9574494984f782cbbe4e52b", "OminousMask");
                RenameAsset("b142c67487db98740b4c0c81e29a8b00", "Capacitor");
                AssetDatabase.SaveAssets();
                File.WriteAllText(Output + "/result.txt", "PASS 유물 3종 및 전용 이미지 2종 이름 변경, GUID 유지, ManaCharger ID 적용");
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/result.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        private static void RenameRelic(string guid, string fileName, string korean, string english)
        {
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(guid));
            if (relic == null) throw new InvalidOperationException("유물 에셋을 찾을 수 없습니다: " + guid);
            RenameAsset(guid, fileName);
            // 마력 충전기는 새 ID를 사용하며 구형 저장 ID는 RelicManager에서 변환합니다.
            if (fileName == "ManaCharger") relic.relicName = "ManaCharger";
            relic.koreanName = korean;
            relic.englishName = english;
            EditorUtility.SetDirty(relic);
            AssetDatabase.SaveAssetIfDirty(relic);

            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationTables.Relics);
            if (collection == null) return;
            foreach (StringTable table in collection.StringTables)
            {
                string code = table.LocaleIdentifier.Code;
                bool isKorean = code.StartsWith("ko", StringComparison.OrdinalIgnoreCase);
                if (!isKorean && !code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) continue;
                table.AddEntry(LocalizationKeys.RelicName(relic.relicName), isKorean ? korean : english);
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssetIfDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssetIfDirty(collection.SharedData);
        }

        private static void RenameAsset(string guid, string name)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("에셋을 찾을 수 없습니다: " + guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return;
            string error = AssetDatabase.RenameAsset(path, name);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
    }
}
