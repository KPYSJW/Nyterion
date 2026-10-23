using System;
using System.IO;
using System.Linq;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>무기 ID와 에셋 참조를 유지하면서 표시 이름과 타격 효과의 정렬을 적용합니다.</summary>
    [InitializeOnLoad]
    public static class WeaponPresentationSetup
    {
        public const string Output = "output/weapon-presentation";
        private const string DataFolder = "Assets/Nytherion/Data/ScriptableObjects/Weapons/";
        private static readonly string[,] Names =
        {
            { "Icicle", "고드름", "Icicle" },
            { "CrimsonLotus", "홍련", "CrimsonLotus" },
            { "Obliteration", "소멸", "Obliteration" },
            { "Corrosion", "부식", "Corrosion" },
            { "Ignition", "인화", "Ignition" },
            { "Isolation", "단절", "Isolation" },
            { "Radiance", "휘광", "Radiance" }
        };

        static WeaponPresentationSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/obliteration-sorting.request"))
            {
                File.Delete(Output + "/obliteration-sorting.request");
                try
                {
                    ApplyBeamSorting();
                    File.WriteAllText(Output + "/obliteration-sorting.txt", "PASS 소멸·홍련 공격 -8, 시작 -7, 타격 100\n");
                }
                catch (Exception error) { File.WriteAllText(Output + "/obliteration-sorting.txt", "FAIL " + error); Debug.LogException(error); }
                return;
            }
            if (!File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Weapons/이름 및 타격 효과 정렬 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/setup.txt", string.Empty);
            for (int i = 0; i < Names.GetLength(0); i++)
            {
                WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataFolder + Names[i, 0] + ".asset");
                if (data == null) throw new InvalidOperationException("무기 데이터 누락: " + Names[i, 0]);
                Undo.RecordObject(data, "무기 이름 변경");
                data.itemName_KR = Names[i, 1];
                data.itemName_EN = Names[i, 2];
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationTables.Items);
                if (collection != null)
                {
                    foreach (var table in collection.StringTables)
                    {
                        string value = table.LocaleIdentifier.Code.StartsWith("ko", StringComparison.OrdinalIgnoreCase)
                            ? data.itemName_KR : data.itemName_EN;
                        string key = LocalizationKeys.ItemName(data.ID);
                        var entry = table.GetEntry(key);
                        if (entry == null) table.AddEntry(key, value);
                        else entry.Value = value;
                        EditorUtility.SetDirty(table);
                        AssetDatabase.SaveAssetIfDirty(table);
                    }
                    EditorUtility.SetDirty(collection.SharedData);
                    AssetDatabase.SaveAssetIfDirty(collection.SharedData);
                }
                File.AppendAllText(Output + "/setup.txt", $"PASS {data.name}: {data.itemName_KR} / {data.itemName_EN}, ID={data.ID}\n");
            }

            // 무기 데이터에서 실제 참조 중인 타격 효과만 수정합니다.
            var effects = AssetDatabase.FindAssets("t:WeaponData", new[] { DataFolder.TrimEnd('/') })
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(data => data != null && data.hitEffectPrefab != null)
                .Select(data => AssetDatabase.GetAssetPath(data.hitEffectPrefab))
                .Concat(new[] { "Assets/Prefabs/Gameplay/Combat/VFX/IcicleHitVFX.prefab" }).Distinct();
            foreach (string path in effects)
                ConfigurePrefab(path, root =>
                {
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                        renderer.sortingOrder = Mathf.Max(WeaponVFXHelper.HitEffectSortingOrder, renderer.sortingOrder);
                });

            ApplyBeamSorting();
            RelicData targetMaker = AssetDatabase.LoadAssetAtPath<RelicData>(
                "Assets/Nytherion/Data/ScriptableObjects/Relics/CombatUtility/TargetMaker.asset");
            if (targetMaker != null)
            {
                targetMaker.description_KR = targetMaker.description_KR.Replace("수호자의 지팡이", "단절");
                EditorUtility.SetDirty(targetMaker);
                AssetDatabase.SaveAssetIfDirty(targetMaker);
            }
            File.AppendAllText(Output + "/setup.txt", "PASS 무기 타격 프리팹 및 광선 정렬 적용\n");
        }

        private static void ConfigurePrefab(string path, Action<GameObject> configure)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ApplyBeamSorting()
        {
            foreach (string path in new[] { "Assets/Prefabs/Gameplay/Combat/Proj/Player_CrimsonLotusBeam.prefab",
                "Assets/Prefabs/Gameplay/Combat/Proj/Player_Obliteration.prefab" })
                ConfigurePrefab(path, root => WeaponVFXHelper.ConfigureBeamSorting(root,
                    root.transform.Find("Visual").GetComponent<Renderer>(),
                    root.transform.Find("StartEffect").GetComponent<SpriteRenderer>(),
                    root.transform.Find("EndEffect").GetComponent<SpriteRenderer>()));
        }
    }
}
