using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class FiveWeaponMuzzleSetup
    {
        public const string Output = "output/weapon-muzzle-links";
        public static readonly string[] Names = { "ArcaneDart", "EvilEye", "GuardianStaff", "Icicle", "VenomousStaff" };
        static readonly int[] ExpectedCounts = { 5, 6, 6, 4, 5 };

        static FiveWeaponMuzzleSetup() => EditorApplication.update += Update;
        static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = Output + "/apply.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/발사 연출/추가한 5종 발사 이미지 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            var data = new WeaponData[Names.Length];
            var frames = new Sprite[Names.Length][];
            for (int i = 0; i < Names.Length; i++)
            {
                data[i] = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/" + Names[i] + ".asset");
                var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nytherion/Art/Combat/" + Names[i] + "MuzzleFlash.png");
                frames[i] = WeaponFireEffectAssets.LoadFrames(sheet);
                if (data[i] == null || frames[i].Length != ExpectedCounts[i])
                    throw new InvalidOperationException(Names[i] + " 무기 데이터 또는 슬라이스 프레임 누락");
            }
            ConfigureGuardianAnchor(data[2]);
            var report = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Names.Length; i++)
            {
                float scale = 0.56f / (frames[i][0].rect.height / frames[i][0].pixelsPerUnit);
                GameObject prefab = WeaponFireEffectAssets.CreateAndAssign(data[i], frames[i], 0, frames[i].Length - 1,
                    60f, scale, Vector3.zero);
                if (Names[i] == "GuardianStaff")
                {
                    string effectPath = AssetDatabase.GetAssetPath(prefab);
                    GameObject effectRoot = PrefabUtility.LoadPrefabContents(effectPath);
                    try
                    {
                        var serializedEffect = new SerializedObject(effectRoot.GetComponent<SpriteMuzzleFlashEffect>());
                        serializedEffect.FindProperty("sortingOrderOffset").intValue = 20;
                        serializedEffect.FindProperty("minimumSortingOrder").intValue = 101;
                        serializedEffect.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(effectRoot, effectPath);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(effectRoot); }
                }
                report.Add("PASS " + Names[i] + ": " + frames[i].Length + "프레임 / 60fps / 크기 " +
                    scale.ToString("0.###") + " / " + AssetDatabase.GetAssetPath(prefab));
            }
            File.WriteAllLines(Output + "/dependencies.txt", AssetDatabase.GetDependencies(
                Names.Select(name => "Assets/Nytherion/Data/ScriptableObjects/Weapons/" + name + ".asset").ToArray(), true));
            File.WriteAllLines(Output + "/setup.txt", report);
            Debug.Log("[FiveWeaponMuzzleSetup] 5종 발사 이미지 연결 완료");
        }

        static void ConfigureGuardianAnchor(WeaponData data)
        {
            string path = AssetDatabase.GetAssetPath(data.weaponPrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var weapon = root.GetComponent<GuardianStaffWeapon>();
                if (weapon == null) throw new InvalidOperationException("GuardianStaff 프리팹 타입이 다릅니다.");
                Transform point = weapon.firePoint != null ? weapon.firePoint : root.transform.Find("FirePoint");
                if (point == null)
                {
                    point = new GameObject("FirePoint").transform;
                    point.SetParent(root.transform, false);
                    point.localPosition = new Vector3(0f, 0.55f, 0f);
                }
                var serialized = new SerializedObject(weapon);
                serialized.FindProperty("firePoint").objectReferenceValue = point;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}

