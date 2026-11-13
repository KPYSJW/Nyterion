using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>파이어 스톰의 원본 시트를 분할하고 스킬·유물·획득 경로를 연결합니다.</summary>
    [InitializeOnLoad]
    public static class FireStormSkillSetup
    {
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/";
        private const string DataPath = Root + "Skill/FireStorm_Skill.asset";
        private const string RelicPath = Root + "Relics/SkillRelics/Relic_FireStorm.asset";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Skills/FireStormSkill.prefab";
        private const string Sprites = "Assets/Nytherion/Art/Skills/Sprites/FireStorm/FireStorm_";

        static FireStormSkillSetup() => EditorApplication.delayCall += SetupIfMissing;

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += SetupIfMissing; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            FireStormSkillData data = AssetDatabase.LoadAssetAtPath<FireStormSkillData>(DataPath);
            if (data == null || data.skillPrefab == null || !data.HasValidFrames) CreateAssets();
        }

        [MenuItem("Tools/Nytherion/Fire Storm/Update Loop Animation")]
        public static void UpdateLoopAnimation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // 원본 파일의 수정 시각이 과거여도 교체한 시트를 다시 가져옵니다.
            AssetDatabase.ImportAsset(Sprites + "Loop.png",
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            FireStormSkillData data = Require<FireStormSkillData>(DataPath);
            data.loopFrames = Slice("Loop", 48, 80, 8, 16, new Vector2(0.5f, 0.075f));
            data.loopFps = 1000f / 70f;
            Save(data);
            Debug.Log("[FireStormSkillSetup] 루프 시트 재임포트 및 16프레임·70ms 애니메이션 연결 완료.", data);
        }

        [MenuItem("Tools/Nytherion/Fire Storm/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            FireStormSkillData data = AssetDatabase.LoadAssetAtPath<FireStormSkillData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<FireStormSkillData>();
                data.skillID = "skill_fire_storm";
                data.skillType = SkillType.FireStorm;
                data.skillName = "파이어 스톰";
                data.description = "마우스 위치에 화염 폭풍을 일으켜 범위 안의 적에게 지속적으로 화염 피해를 줍니다.";
                data.skillLevel = 1;
                data.damage = 10f;
                data.coolDown = 8f;
                data.manaCost = 20;
                data.targetLayers = LayerMask.GetMask("Enemy");
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.spawnFrames = Slice("Spawn", 48, 80, 8, 8, new Vector2(0.5f, 0.075f));
            data.loopFrames = Slice("Loop", 48, 80, 8, 16, new Vector2(0.5f, 0.075f));
            // 루프 원본의 프레임당 70ms 재생 속도를 유지합니다.
            data.loopFps = 1000f / 70f;
            data.hitFrames = Slice("Hit", 64, 64, 7, 7, new Vector2(0.5f, 0.5f));
            string iconPath = "Assets/Nytherion/Art/Skills/Sprites/FireStorm/FireStorm_Icon.png";
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter iconImporter = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            Configure(iconImporter, SpriteImportMode.Single);
            iconImporter.SaveAndReimport();
            data.icon = Require<Sprite>(iconPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject("FireStormSkill");
                    SceneManager.MoveGameObjectToScene(root, preview);
                    root.AddComponent<FireStormSkill>().skillData = data;
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            data.skillPrefab = Require<GameObject>(PrefabPath);
            Save(data);
            SkillDatabaseSO database = Require<SkillDatabaseSO>(Root + "Skill/SkillDatabaseSO.asset");
            if (!database.allSkills.Contains(data)) { database.allSkills.Add(data); Save(database); }

            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Relic of Fire Storm";
                relic.koreanName = "파이어 스톰 마법 책";
                relic.englishName = "Fire Storm Magic Book";
                relic.description_KR = "범위 안의 적에게 화염 피해를 주는 파이어 스톰 스킬을 얻는다.";
                relic.description_EN = "Grants Fire Storm, which deals fire damage to enemies in an area.";
                relic.Image = data.icon;
                relic.rarity = Rarity.Rare;
                relic.effectModules = new List<RelicEffectModule>
                { new RelicEffectModule { effects = new List<RelicEffectBase> { new GrantSkillEffect { skillData = data } } } };
                AssetDatabase.CreateAsset(relic, RelicPath);
            }
            RelicInfluencePolicy.Normalize(relic);
            Save(relic);
            RelicDatabaseSO relicDatabase = Require<RelicDatabaseSO>(Root + "Relics/RelicDatabase.asset");
            if (!relicDatabase.allRelics.Contains(relic)) { relicDatabase.allRelics.Add(relic); Save(relicDatabase); }
            GachaPoolSO pool = Require<GachaPoolSO>(Root + $"Gacha/GachaPool/Relic/{relic.rarity}_Relic.asset");
            if (!pool.items.Any(entry => entry.item == relic))
            { pool.items.Add(new GachaItemRate { item = relic, weight = 100 }); Save(pool); }
            Directory.CreateDirectory("output/fire-storm");
            File.WriteAllText("output/fire-storm/setup-result.txt",
                "FireStorm setup OK: spawn=8, loop=16, hit=7; prefab, skill database, relic database, rare gacha connected.");
            Debug.Log("[FireStormSkillSetup] 파이어 스톰 스킬과 8/16/7프레임 애니메이션 연결 완료.", data);
        }

        private static Sprite[] Slice(string suffix, int width, int height, int columns, int count, Vector2 pivot)
        {
            string path = Sprites + suffix + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Texture2D texture = Require<Texture2D>(path);
            if (texture.width != width * columns || texture.height != height * Mathf.CeilToInt(count / (float)columns))
                throw new InvalidOperationException("스프라이트 시트 크기가 예상과 다릅니다: " + path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Configure(importer, SpriteImportMode.Multiple);
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] old = provider.GetSpriteRects();
            SpriteRect[] rects = Enumerable.Range(0, count).Select(i =>
            {
                string name = "FireStorm_" + suffix + "_" + i.ToString("D2");
                SpriteRect previous = old.FirstOrDefault(rect => rect.name == name);
                return new SpriteRect { name = name,
                    rect = new Rect(i % columns * width, texture.height - (i / columns + 1) * height, width, height),
                    alignment = SpriteAlignment.Custom, pivot = pivot,
                    spriteID = previous != null ? previous.spriteID : GUID.Generate() };
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            if (frames.Length != count) throw new InvalidOperationException("프레임 분할에 실패했습니다: " + path);
            return frames;
        }

        private static void Configure(TextureImporter importer, SpriteImportMode mode)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new MissingReferenceException("필수 에셋이 없습니다: " + path);
        private static void Save(Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
    }
}

