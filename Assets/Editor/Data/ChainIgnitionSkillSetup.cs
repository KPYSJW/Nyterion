using System;
using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>첨부 원본을 슬라이스하고 스킬, 폭발 프리팹 및 획득 경로를 연결합니다.</summary>
    [InitializeOnLoad]
    public static class ChainIgnitionSkillSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Skill/ChainIgnition_Skill.asset";
        public const string SkillPrefabPath = "Assets/Prefabs/Gameplay/Skills/ChainIgnitionSkill.prefab";
        public const string WavePrefabPath = "Assets/Prefabs/Gameplay/Skills/ChainIgnitionWave.prefab";
        private const string SheetPath = "Assets/Nytherion/Art/Skills/Sprites/ChainIgnition.png";
        private const string IconPath = "Assets/Nytherion/Art/Skills/Sprites/ChainIgnition_Icon.png";
        public const string SoundPath = "Assets/Nytherion/Audio/ChainIgnitionSound.wav";
        private const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Relic_ChainIgnition.asset";

        static ChainIgnitionSkillSetup()
        {
            EditorApplication.delayCall += SetupIfMissing;
        }

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += SetupIfMissing;
                return;
            }
            ChainIgnitionSkillData data = AssetDatabase.LoadAssetAtPath<ChainIgnitionSkillData>(DataPath);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (data == null && sheet != null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath) != null)
            {
                CreateAssets();
            }
            else if (data != null && sheet != null &&
                (data.explosionFrames == null || data.explosionFrames.Length != GetFrameCount(sheet) ||
                 data.explosionFrames.Any(frame => frame == null || frame.rect.width != sheet.height || frame.rect.height != sheet.height)))
            {
                UpdateExplosionAnimation();
            }
            if (data != null && AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath) != null &&
                (data.explosionSound == null || (data.wavePrefab != null && data.wavePrefab.GetComponent<AudioSource>() == null)))
                UpdateExplosionSound();
        }

        [MenuItem("Tools/Nytherion/Chain Ignition/Update Explosion Sound")]
        public static void UpdateExplosionSound()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ChainIgnitionSkillData data = Require<ChainIgnitionSkillData>(DataPath);
            if (data.explosionSound == null) data.explosionSound = Require<AudioClip>(SoundPath);
            GameObject root = PrefabUtility.LoadPrefabContents(WavePrefabPath);
            try
            {
                AudioSource source = root.GetComponent<AudioSource>();
                if (source == null) source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                PrefabUtility.SaveAsPrefabAsset(root, WavePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Save(data);
            Debug.Log("[ChainIgnitionSkillSetup] 연쇄 점화 각 파동의 효과음 연결 완료.", data);
        }

        [MenuItem("Tools/Nytherion/Chain Ignition/Update Explosion Animation")]
        public static void UpdateExplosionAnimation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ChainIgnitionSkillData data = Require<ChainIgnitionSkillData>(DataPath);
            data.explosionFrames = ConfigureFrames();
            Save(data);
            Debug.Log($"[ChainIgnitionSkillSetup] 연쇄 점화 폭발 애니메이션 {data.explosionFrames.Length}프레임 연결 완료.", data);
        }

        [MenuItem("Tools/Nytherion/Chain Ignition/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SkillDatabaseSO database = Require<SkillDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset");
            GachaPoolSO skillPool = Require<GachaPoolSO>("Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Skill/SkillGachaPool.asset");
            RelicDatabaseSO relicDatabase = Require<RelicDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset");
            GachaPoolSO relicPool = Require<GachaPoolSO>("Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Relic/Common_Relic.asset");
            Sprite[] frames = ConfigureFrames();
            Sprite icon = ConfigureIcon();

            ChainIgnitionSkillData data = AssetDatabase.LoadAssetAtPath<ChainIgnitionSkillData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ChainIgnitionSkillData>();
                data.skillID = "skill_chain_ignition";
                data.skillType = SkillType.ChainIgnition;
                data.skillName = "연쇄 점화";
                data.description = "마우스와 가까운 방향으로 화염 폭발이 세 차례 퍼집니다. 기본 1방향에서 2레벨마다 1방향씩 증가하며 유물 보정을 포함해 최대 8방향까지 폭발합니다. 레벨마다 기본 피해가 2 증가하고 투사체 수, 크기, 범위 증가 유물이 적용됩니다.";
                data.skillLevel = 1;
                data.coolDown = 6f;
                data.damage = 10f;
                data.range = 3f;
                data.targetLayers = LayerMask.GetMask("Enemy");
                AssetDatabase.CreateAsset(data, DataPath);
            }
            // 재실행할 때 사용자가 조정한 밸런스 값은 보존합니다.
            data.icon = icon;
            data.explosionFrames = frames;
            GameObject wavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WavePrefabPath);
            if (wavePrefab == null) wavePrefab = CreateWavePrefab(frames[0]);
            data.wavePrefab = wavePrefab.GetComponent<ChainIgnitionWave>();
            if (data.wavePrefab == null) throw new MissingComponentException("폭발 프리팹에 ChainIgnitionWave가 없습니다.");
            GameObject skillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPrefabPath);
            if (skillPrefab == null) skillPrefab = CreateSkillPrefab(data);
            data.skillPrefab = skillPrefab;
            if (data.explosionSound == null) data.explosionSound = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
            Save(data);
            if (data.explosionSound != null) UpdateExplosionSound();

            if (database.allSkills == null) database.allSkills = new List<SkillData>();
            if (!database.allSkills.Contains(data))
            {
                database.allSkills.Add(data);
                Save(database);
            }
            AddToPool(skillPool, data, 1);

            // 기존 스킬 각인과 같은 GrantSkillEffect를 사용합니다.
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Relic of Chain Ignition";
                relic.koreanName = "연쇄 점화 각인";
                relic.description_KR = "[연쇄 점화] 스킬을 얻습니다.\n[연쇄 점화] : " + data.description;
                relic.description_EN = "Grants Chain Ignition. Three waves expand toward the mouse, starting with one direction. Levels and relics increase damage and fill up to eight directions.";
                relic.Image = icon;
                relic.rarity = Rarity.Common;
                relic.effectModules = new List<RelicEffectModule>
                {
                    new RelicEffectModule { effects = new List<RelicEffectBase> { new GrantSkillEffect { skillData = data } } }
                };
                AssetDatabase.CreateAsset(relic, RelicPath);
            }
            if (relicDatabase.allRelics == null) relicDatabase.allRelics = new List<RelicData>();
            if (!relicDatabase.allRelics.Contains(relic))
            {
                relicDatabase.allRelics.Add(relic);
                Save(relicDatabase);
            }
            AddToPool(relicPool, relic, 100);
            Debug.Log($"[ChainIgnitionSkillSetup] 연쇄 점화 스킬, {frames.Length}프레임 애니메이션, 아이콘과 획득 경로 연결 완료.", data);
        }

        private static Sprite[] ConfigureFrames()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (importer == null) throw new MissingReferenceException("ChainIgnition.png 원본이 없습니다.");
            Texture2D sheet = Require<Texture2D>(SheetPath);
            int frameSize = sheet.height;
            int frameCount = GetFrameCount(sheet);
            // 해상도가 바뀌어도 한 프레임의 월드 크기와 기존 Visual Scale을 유지합니다.
            ConfigureImporter(importer, SpriteImportMode.Multiple, frameSize);
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            List<SpriteNameFileIdPair> previous = names.GetNameFileIdPairs().ToList();
            SpriteRect[] previousRects = provider.GetSpriteRects();
            SpriteRect pivotReference = previousRects.FirstOrDefault();
            SpriteRect[] rects = Enumerable.Range(0, frameCount).Select(index =>
            {
                string name = "ChainIgnition_" + index;
                SpriteNameFileIdPair pair = previous.FirstOrDefault(item => item.name == name);
                SpriteRect previousRect = previousRects.FirstOrDefault(rect => rect.name == name) ?? pivotReference;
                return new SpriteRect
                {
                    name = name, rect = new Rect(index * frameSize, 0, frameSize, frameSize),
                    alignment = previousRect != null ? previousRect.alignment : SpriteAlignment.Center,
                    pivot = previousRect != null ? previousRect.pivot : new Vector2(0.5f, 0.5f),
                    spriteID = pair != null ? pair.GetFileGUID() : GUID.Generate()
                };
            }).ToArray();
            provider.SetSpriteRects(rects);
            names.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
        }

        private static int GetFrameCount(Texture2D sheet)
        {
            if (sheet.height <= 0 || sheet.width % sheet.height != 0)
                throw new InvalidOperationException("연쇄 점화 스프라이트는 정사각형 프레임을 가로 한 줄로 배치해 주세요.");
            return sheet.width / sheet.height;
        }

        private static Sprite ConfigureIcon()
        {
            TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null) throw new MissingReferenceException("ChainIgnition_Icon.png 원본이 없습니다.");
            ConfigureImporter(importer, SpriteImportMode.Single);
            return Require<Sprite>(IconPath);
        }

        private static void ConfigureImporter(TextureImporter importer, SpriteImportMode mode, float pixelsPerUnit = 32f)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static GameObject CreateWavePrefab(Sprite firstFrame)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("ChainIgnitionWave");
                SceneManager.MoveGameObjectToScene(root, preview);
                ChainIgnitionWave wave = root.AddComponent<ChainIgnitionWave>();
                SerializedObject serialized = new SerializedObject(wave);
                SerializedProperty renderers = serialized.FindProperty("explosionRenderers");
                renderers.arraySize = ChainIgnitionSkillData.WaveCount * ChainIgnitionSkillData.DirectionCount;
                for (int i = 0; i < renderers.arraySize; i++)
                {
                    GameObject child = new GameObject($"Explosion_{i / 8 + 1}_{i % 8 + 1}");
                    child.transform.SetParent(root.transform, false);
                    SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
                    renderer.sprite = firstFrame;
                    renderer.sortingOrder = 10;
                    renderer.enabled = false;
                    renderers.GetArrayElementAtIndex(i).objectReferenceValue = renderer;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, WavePrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static GameObject CreateSkillPrefab(ChainIgnitionSkillData data)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("ChainIgnitionSkill");
                SceneManager.MoveGameObjectToScene(root, preview);
                root.AddComponent<ChainIgnitionSkill>().skillData = data;
                return PrefabUtility.SaveAsPrefabAsset(root, SkillPrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static T Require<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new MissingReferenceException("필수 에셋이 없습니다: " + path);
            return asset;
        }

        private static void AddToPool(GachaPoolSO pool, ScriptableObject item, int weight)
        {
            if (pool.items == null) pool.items = new List<GachaItemRate>();
            if (pool.items.Any(entry => entry.item == item)) return;
            pool.items.Add(new GachaItemRate { item = item, weight = weight });
            Save(pool);
        }

        private static void Save(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
