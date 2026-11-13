using System;
using System.IO;
using System.Linq;
using Nytherion.GamePlay.Characters.Companions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class OpapaDashEffectSetup
    {
        private const string SheetPath = "Assets/Nytherion/Art/Characters/Companions/Opapa/Sprites/DashStart.png";
        private const string MaterialPath = "Assets/Nytherion/Art/Characters/Companions/Opapa/Sprites/Mat_DashStart.mat";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Characters/Companions/Opapa/Opapa.prefab";
        private const string Output = "output/opapa-dash";

        static OpapaDashEffectSetup() => EditorApplication.update += ProcessRequest;

        private static void ProcessRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Opapa/돌진 출발 이펙트 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Configure(root.GetComponent<OpapaCompanion>());
                SerializedObject settings = new SerializedObject(root.GetComponent<OpapaCompanion>());
                settings.FindProperty("dashStartBehindOffset").floatValue = 0.05f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            SerializedObject verification = new SerializedObject(saved.GetComponent<OpapaCompanion>());
            Sprite[] frames = LoadFrames();
            SerializedProperty savedFrames = verification.FindProperty("dashStartFrames");
            if (savedFrames.arraySize != 8 || frames.Length != 8 ||
                verification.FindProperty("dashStartMaterial").objectReferenceValue == null ||
                !Mathf.Approximately(verification.FindProperty("dashStartBehindOffset").floatValue, 0.05f))
                throw new InvalidOperationException("Opapa 출발 먼지 참조 검증 실패");
            for (int i = 0; i < frames.Length; i++)
                if (savedFrames.GetArrayElementAtIndex(i).objectReferenceValue != frames[i])
                    throw new InvalidOperationException("Opapa 출발 먼지 프레임 순서 오류: " + i);
            File.WriteAllText(Output + "/verification.txt",
                "PASS 256×128 RGBA 원본, 4열×2행 64×64 8프레임, Point/무압축, URP Unlit, Opapa 프리팹 프레임 순서 및 재질 참조 검증\n");
            Debug.Log("[OpapaDashEffectSetup] 돌진 출발 이펙트 적용 완료.");
        }

        internal static void Configure(OpapaCompanion companion)
        {
            if (companion == null) throw new MissingComponentException("Opapa 컴포넌트가 없습니다.");
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (texture == null || texture.width != 256 || texture.height != 128)
                throw new InvalidOperationException("Opapa 출발 먼지 시트는 256×128 원본이어야 합니다.");

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] previous = provider.GetSpriteRects();
            SpriteRect[] rects = new SpriteRect[8];
            for (int i = 0; i < rects.Length; i++)
            {
                string name = "Opapa_DashStart_" + i.ToString("D2");
                SpriteRect existing = previous.FirstOrDefault(rect => rect.name == name);
                rects[i] = new SpriteRect
                {
                    name = name,
                    rect = new Rect(i % 4 * 64, 128 - (i / 4 + 1) * 64, 64, 64),
                    alignment = SpriteAlignment.Custom,
                    // 불투명 픽셀의 바닥(y=50)과 오른쪽 끝을 출발 기준점으로 사용합니다.
                    pivot = new Vector2(59f / 64f, 14f / 64f),
                    spriteID = existing != null ? existing.spriteID : GUID.Generate()
                };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("URP 2D Unlit 셰이더가 없습니다.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            Sprite[] frames = LoadFrames();
            if (frames.Length != 8) throw new InvalidOperationException("출발 먼지 8프레임 분할 실패");
            SerializedObject serialized = new SerializedObject(companion);
            SerializedProperty array = serialized.FindProperty("dashStartFrames");
            array.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            serialized.FindProperty("dashStartMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite[] LoadFrames() => AssetDatabase.LoadAllAssetsAtPath(SheetPath)
            .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
    }
}
