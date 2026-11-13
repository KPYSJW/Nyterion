using System;
using System.IO;
using System.Linq;
using Nytherion.GamePlay.Characters.Player;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class DashSpriteSheetSetup
    {
        private const string ArtRoot = "Assets/Nytherion/Art/Characters/Player/Effects/";
        private const string SheetPath = ArtRoot + "DashSpriteSheet.png";
        private const string PlayerPath = "Assets/Prefabs/Gameplay/Characters/Player/Player.prefab";
        private const string Output = "output/dash-sprite-sheet";

        static DashSpriteSheetSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Player/대쉬 스프라이트 시트 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (texture == null || texture.width != 512 || texture.height != 96)
                throw new InvalidOperationException("대쉬 시트는 512×96 원본이어야 합니다.");

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
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
                string name = "Dash_" + i.ToString("D2");
                SpriteRect existing = previous.FirstOrDefault(rect => rect.name == name);
                rects[i] = new SpriteRect
                {
                    name = name,
                    rect = new Rect(i % 4 * 128, 96 - (i / 4 + 1) * 48, 128, 48),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(22f / 128f, 0.5f),
                    spriteID = existing != null ? existing.spriteID : GUID.Generate()
                };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
                .OrderBy(sprite => sprite.name).ToArray();
            if (frames.Length != 8) throw new InvalidOperationException("대쉬 8프레임 분할 실패");

            string materialPath = ArtRoot + "Mat_DashSpriteSheet.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("URP 2D Unlit 셰이더를 찾을 수 없습니다.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                DashAfterimageVFX effect = player.GetComponent<DashAfterimageVFX>();
                if (effect == null) throw new MissingComponentException("Player의 대쉬 잔상 컴포넌트가 없습니다.");
                SerializedObject serialized = new SerializedObject(effect);
                serialized.FindProperty("useSpriteSheet").boolValue = true;
                serialized.FindProperty("spriteSheetMaterial").objectReferenceValue = material;
                SerializedProperty array = serialized.FindProperty("dashFrames");
                array.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            SerializedObject verification = new SerializedObject(saved.GetComponent<DashAfterimageVFX>());
            SerializedProperty savedFrames = verification.FindProperty("dashFrames");
            if (savedFrames.arraySize != 8 || !verification.FindProperty("useSpriteSheet").boolValue ||
                verification.FindProperty("spriteSheetMaterial").objectReferenceValue == null)
                throw new InvalidOperationException("Player 프리팹의 시트 연결 검증 실패");
            for (int i = 0; i < 8; i++)
                if (savedFrames.GetArrayElementAtIndex(i).objectReferenceValue != frames[i])
                    throw new InvalidOperationException("프레임 순서 또는 참조 누락: " + i);
            File.WriteAllText(Output + "/setup.txt",
                "PASS 원본 RGBA 512×96, 4열×2행 128×48 8프레임, Point/무압축, URP Unlit, Player 프리팹 참조 및 순서 검증\n");
            Debug.Log("[DashSpriteSheetSetup] 기존 대쉬에 스프라이트 시트 적용 완료.");
        }
    }
}
