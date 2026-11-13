using System;
using System.IO;
using Nytherion.Data.ScriptableObjects;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class BurnTickEffectSetup
    {
        private const string Output = "output/burn-tick";
        private const string FlamePath = "Assets/Nytherion/Art/Combat/VFX/Sprites/StatusEffects/BurnFlameSheet.png";
        private const string EmberPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/StatusEffects/BurnEmberSheet.png";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/StatusEffects/BurnVariantSheet.png";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/BurnTickVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/StatusEffectDatabase.asset";

        static BurnTickEffectSetup() => EditorApplication.update += ProcessRequest;

        private static void ProcessRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/two-variant-setup.request")) return;
            File.Delete(Output + "/two-variant-setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/상태이상/화상 피해 이펙트 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            BuildAnimationSheet();
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            StatusEffectDatabase data = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(DataPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (sheet == null || data == null || prefab == null || prefab.GetComponent<BurnTickVFX>() == null)
                throw new InvalidOperationException("화상 시트, 프리팹과 상태이상 데이터베이스를 확인해 주세요.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                BurnTickVFX effect = root.GetComponent<BurnTickVFX>();
                SerializedObject serialized = new SerializedObject(effect);
                serialized.FindProperty("animationSheet").objectReferenceValue = sheet;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (SpriteRenderer oldFlame in root.GetComponentsInChildren<SpriteRenderer>(true)) oldFlame.enabled = false;
                effect.enabled = false;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                BurnTickVFX savedEffect = saved.GetComponent<BurnTickVFX>();
                if (data.burnTickPrefab != savedEffect)
                {
                    data.burnTickPrefab = savedEffect;
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssetIfDirty(data);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Verify();
            Debug.Log("[BurnTickEffectSetup] 화상 애니메이션 2종을 무작위 행 파티클로 연결했습니다.", data);
        }

        private static void BuildAnimationSheet()
        {
            // 원본의 위쪽 행부터 왼쪽→오른쪽 순서로 읽어 종류마다 한 행으로 배치합니다.
            Texture2D atlas = new Texture2D(320, 64, TextureFormat.RGBA32, false);
            try
            {
                string[] paths = { FlamePath, EmberPath };
                for (int variant = 0; variant < paths.Length; variant++)
                {
                    Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        if (!source.LoadImage(File.ReadAllBytes(paths[variant])) || source.width != 160 || source.height != 64)
                            throw new InvalidOperationException("화상 원본은 160×64, 32×32 프레임 10장이어야 합니다: " + paths[variant]);
                        for (int frame = 0; frame < 10; frame++)
                            atlas.SetPixels(frame * 32, (1 - variant) * 32, 32, 32,
                                source.GetPixels(frame % 5 * 32, (1 - frame / 5) * 32, 32, 32));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(source); }
                }
                atlas.Apply();
                File.WriteAllBytes(SheetPath, atlas.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        public static void Verify()
        {
            StatusEffectDatabase data = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(DataPath);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (data == null || data.burnTickPrefab == null || sheet == null || sheet.width != 320 || sheet.height != 64)
                throw new InvalidOperationException("화상 프리팹 참조 또는 10열×2행 시트 검증 실패");
            SerializedObject configured = new SerializedObject(data.burnTickPrefab);
            if (configured.FindProperty("animationSheet").objectReferenceValue != sheet)
                throw new InvalidOperationException("화상 애니메이션 시트 참조가 올바르지 않습니다.");
            File.WriteAllText(Output + "/verification.txt", "PASS 화상 애니메이션 2종×10프레임, 프리팹 및 데이터베이스 참조\n");
        }
    }
}
