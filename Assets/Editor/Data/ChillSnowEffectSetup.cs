using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ChillSnowEffectSetup
    {
        private const string Output = "output/chill-snow";
        private const string FirstVariantPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/StatusEffects/ChillCrystalA.png";
        private const string SecondVariantPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/StatusEffects/ChillCrystalB.png";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/StatusEffects/ChillCrystalVariants.png";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/ChillSnowVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/StatusEffectDatabase.asset";

        static ChillSnowEffectSetup() => EditorApplication.update += ProcessRequest;

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

        [MenuItem("Tools/Nytherion/상태이상/냉기 눈 결정 이펙트 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            BuildAnimationSheet();
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            StatusEffectDatabase data = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(DataPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (sheet == null || data == null || prefab == null || prefab.GetComponent<ChillSnowVFX>() == null)
                throw new InvalidOperationException("냉기 시트, 프리팹과 상태이상 데이터베이스를 확인해 주세요.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ChillSnowVFX effect = root.GetComponent<ChillSnowVFX>();
                SerializedObject serialized = new SerializedObject(effect);
                serialized.FindProperty("animationSheet").objectReferenceValue = sheet;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (SpriteRenderer oldRenderer in root.GetComponentsInChildren<SpriteRenderer>(true)) oldRenderer.enabled = false;
                effect.enabled = false;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                ChillSnowVFX savedEffect = saved.GetComponent<ChillSnowVFX>();
                if (data.chillSnowPrefab != savedEffect)
                {
                    data.chillSnowPrefab = savedEffect;
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssetIfDirty(data);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Verify();
            Debug.Log("[ChillSnowEffectSetup] 냉기 애니메이션 2종을 무작위 행 파티클로 연결했습니다.", data);
        }

        private static void BuildAnimationSheet()
        {
            // 원본의 위쪽 행부터 왼쪽→오른쪽 순서로 읽어 종류마다 한 행으로 배치합니다.
            Texture2D atlas = new Texture2D(320, 64, TextureFormat.RGBA32, false);
            try
            {
                string[] paths = { FirstVariantPath, SecondVariantPath };
                for (int variant = 0; variant < paths.Length; variant++)
                {
                    Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        if (!source.LoadImage(File.ReadAllBytes(paths[variant])) || source.width != 160 || source.height != 64)
                            throw new InvalidOperationException("냉기 원본은 160×64, 32×32 프레임 10장이어야 합니다: " + paths[variant]);
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
            if (data == null || data.chillSnowPrefab == null || sheet == null || sheet.width != 320 || sheet.height != 64)
                throw new InvalidOperationException("냉기 프리팹 참조 또는 10열×2행 시트 검증 실패");
            SerializedObject configured = new SerializedObject(data.chillSnowPrefab);
            if (configured.FindProperty("animationSheet").objectReferenceValue != sheet)
                throw new InvalidOperationException("냉기 애니메이션 시트 참조가 올바르지 않습니다.");
            VerifyBodyColumns();
            File.WriteAllText(Output + "/verification.txt", "PASS 냉기 애니메이션 2종×10프레임, 프리팹 및 데이터베이스 참조, 가로 위치별 경사 윤곽·범위 밖·상하/좌우 뒤집기·이동 및 배율\n");
        }

        private static void VerifyBodyColumns()
        {
            GameObject root = new GameObject("냉기 윤곽 검증") { hideFlags = HideFlags.HideAndDontSave };
            Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f, 1f);
            try
            {
                SpriteRenderer body = root.AddComponent<SpriteRenderer>();
                body.sprite = sprite;
                ChillSnowVFX effect = root.AddComponent<ChillSnowVFX>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ChillSnowVFX).GetField("body", flags).SetValue(effect, body);
                // 외곽선 추출과 분리하여, 알려진 경사 윤곽의 교차점과 좌표 변환을 검증합니다.
                var cache = (Dictionary<Sprite, Vector2[][]>)typeof(ChillSnowVFX).GetField("outlineCache", flags).GetValue(effect);
                cache.Add(sprite, new[] { new[]
                {
                    new Vector2(-1f, -1f), new Vector2(1f, -1f),
                    new Vector2(0.5f, 1f), new Vector2(-0.5f, 1f)
                } });
                MethodInfo update = typeof(ChillSnowVFX).GetMethod("UpdateBodyOutlines", flags);
                MethodInfo range = typeof(ChillSnowVFX).GetMethod("TryGetColumnRange", flags);
                update.Invoke(effect, null);
                AssertColumn(effect, range, 0f, -1f, 1f);
                AssertColumn(effect, range, 0.75f, -1f, 0f);
                object[] outside = { 1.1f, 0f, 0f };
                if ((bool)range.Invoke(effect, outside))
                    throw new InvalidOperationException("윤곽 밖의 가로 위치를 허용했습니다.");
                body.flipY = true;
                body.flipX = true;
                update.Invoke(effect, null);
                AssertColumn(effect, range, 0.75f, 0f, 1f);
                body.flipY = false;
                root.transform.position = new Vector3(4f, 5f, 0f);
                root.transform.localScale = new Vector3(-2f, 3f, 1f);
                update.Invoke(effect, null);
                AssertColumn(effect, range, 5.5f, 2f, 5f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sprite);
            }
        }

        private static void AssertColumn(ChillSnowVFX effect, MethodInfo method, float x, float bottom, float top)
        {
            object[] arguments = { x, 0f, 0f };
            if (!(bool)method.Invoke(effect, arguments) || Mathf.Abs((float)arguments[1] - bottom) > 0.001f ||
                Mathf.Abs((float)arguments[2] - top) > 0.001f)
                throw new InvalidOperationException("가로 위치별 윤곽 검증 실패: " + x);
        }
    }
}


