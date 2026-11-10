using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>독방울의 원본 시트를 분할하고 스킬·유물·획득 경로를 연결합니다.</summary>
    [InitializeOnLoad]
    public static class PoisonDropSkillSetup
    {
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/";
        private const string DataPath = Root + "Skill/PoisonDrop_Skill.asset";
        private const string RelicPath = Root + "Relics/SkillRelics/Relic_PoisonDrop.asset";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Skills/PoisonDropSkill.prefab";
        private const string Sprites = "Assets/Nytherion/Art/Skills/Sprites/PoisonDrop_";

        static PoisonDropSkillSetup() => EditorApplication.delayCall += SetupIfMissing;

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += SetupIfMissing; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            PoisonDropSkillData data = AssetDatabase.LoadAssetAtPath<PoisonDropSkillData>(DataPath);
            if (data == null || data.skillPrefab == null || !data.HasValidFrames) CreateAssets();
            SyncLocalization();
            VerifyAssets();
        }

        [MenuItem("Tools/Nytherion/Poison Drop/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            PoisonDropSkillData data = AssetDatabase.LoadAssetAtPath<PoisonDropSkillData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<PoisonDropSkillData>();
                data.skillID = "skill_poison_drop";
                data.skillType = SkillType.PoisonDrop;
                data.skillName = "독방울";
                data.description = "독방울을 떨어뜨려 범위 안의 적에게 피해와 독 효과를 부여합니다.";
                data.skillLevel = 1;
                data.damage = 20f;
                data.coolDown = 6f;
                data.manaCost = 15;
                data.targetLayers = LayerMask.GetMask("Enemy");
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.dropSprite = Slice("Drop", 96, 72, 1, 1, new Vector2(0.5f, 30f / 72f))[0];
            data.frames = Slice("Sheet", 96, 72, 3, 13, new Vector2(0.5f, 12f / 72f));
            string iconPath = Sprites + "Icon.png";
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
                    GameObject root = new GameObject("PoisonDropSkill");
                    SceneManager.MoveGameObjectToScene(root, preview);
                    root.AddComponent<PoisonDropSkill>().skillData = data;
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
                relic.relicName = "Relic of Poison Drop";
                relic.koreanName = "독방울 마법 책";
                relic.englishName = "Poison Drop Magic Book";
                relic.description_KR = "독방울 스킬을 얻는다.";
                relic.description_EN = "Grants the Poison Drop skill.";
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
            SyncLocalization();
            VerifyAssets();
            Directory.CreateDirectory("output/poison-drop");
            File.WriteAllText("output/poison-drop/setup-result.txt",
                "PoisonDrop setup OK: moving single drop, 13 non-looping impact/puddle/fade frames; prefab, skill database, relic database, rare gacha connected.");
            Debug.Log("[PoisonDropSkillSetup] 독방울 단일 이미지 낙하와 착탄·웅덩이 13프레임 연결 완료.", data);
        }

        private static void SyncLocalization()
        {
            Sync(LocalizationTables.Skills, LocalizationKeys.SkillName("skill_poison_drop"), "독방울", "Poison Drop");
            Sync(LocalizationTables.Skills, LocalizationKeys.SkillDescription("skill_poison_drop"),
                "독방울을 떨어뜨려 범위 안의 적에게 피해와 독 효과를 부여합니다.",
                "Drops a poison glob that damages and poisons enemies in an area.");
            Sync(LocalizationTables.Relics, LocalizationKeys.RelicName("Relic of Poison Drop"),
                "독방울 마법 책", "Poison Drop Magic Book");
            Sync(LocalizationTables.Relics, LocalizationKeys.RelicDescription("Relic of Poison Drop"),
                "독방울 스킬을 얻는다.", "Grants the Poison Drop skill.");
        }

        private static void Sync(string tableName, string key, string korean, string english)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (collection == null) return;
            foreach (StringTable table in collection.StringTables)
            {
                string locale = table.LocaleIdentifier.Code;
                if (locale != "ko" && locale != "en") continue;
                string value = locale == "ko" ? korean : english;
                var entry = table.GetEntry(key);
                if (entry != null && entry.Value == value) continue;
                if (entry == null) table.AddEntry(key, value);
                else entry.Value = value;
                Save(table);
            }
            if (EditorUtility.IsDirty(collection.SharedData)) Save(collection.SharedData);
        }

        [MenuItem("Tools/Nytherion/Poison Drop/Verify Assets")]
        public static void VerifyAssets()
        {
            PoisonDropSkillData data = Require<PoisonDropSkillData>(DataPath);
            RelicData relic = Require<RelicData>(RelicPath);
            PoisonDropSkill skill = Require<GameObject>(PrefabPath).GetComponent<PoisonDropSkill>();
            if (!data.HasValidFrames || data.icon == null || data.skillPrefab == null ||
                skill == null || skill.skillData != data || data.targetLayers.value == 0 ||
                !Require<SkillDatabaseSO>(Root + "Skill/SkillDatabaseSO.asset").allSkills.Contains(data) ||
                !Require<RelicDatabaseSO>(Root + "Relics/RelicDatabase.asset").allRelics.Contains(relic) ||
                !relic.effectModules.Any(module => module.effects.Any(effect => effect is GrantSkillEffect grant && grant.skillData == data)) ||
                !Require<GachaPoolSO>(Root + $"Gacha/GachaPool/Relic/{relic.rarity}_Relic.asset").items.Any(entry => entry.item == relic))
                throw new InvalidOperationException("독방울의 프레임, 프리팹 또는 획득 경로 연결을 확인하세요.");
            Directory.CreateDirectory("output/poison-drop");
            File.WriteAllText("output/poison-drop/asset-verification.txt", "PASS: single drop sprite, 13 impact frames, icon, prefab/data references, enemy layer, skill/relic databases, grant effect, gacha.\n");
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
                string name = "PoisonDrop_" + suffix + "_" + i.ToString("D2");
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

