using System;
using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    public static class DarkboltSkillSetup
    {
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/";
        private const string DataPath = Root + "Skill/Darkbolt_Skill.asset";
        private const string RelicPath = Root + "Relics/SkillRelics/Relic_Darkbolt.asset";
        private const string SkillPath = "Assets/Prefabs/Gameplay/Skills/DarkboltSkill.prefab";
        private const string ProjectilePath = "Assets/Prefabs/Gameplay/Skills/DarkboltProjectile.prefab";
        private const string Sprites = "Assets/Nytherion/Art/Skills/Sprites/DarkBolt_";
        private const string TailPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/DarkboltTail.png";

        [MenuItem("Tools/Nytherion/Darkbolt/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Sprite[] projectileFrames = { ConfigureIcon(Sprites + "Projectile.png") };
            Sprite[] hitFrames = ConfigureFrames(Sprites + "Hit.png", 64, 7);
            Sprite[] castFrames = ConfigureFrames(Sprites + "Cast.png", 64, 5);
            Sprite icon = ConfigureIcon(Sprites + "Icon.png");

            DarkboltSkillData data = AssetDatabase.LoadAssetAtPath<DarkboltSkillData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DarkboltSkillData>();
                data.skillID = "skill_darkbolt";
                data.skillType = SkillType.DarkBolt;
                data.skillName = "다크볼트";
                data.description = "적에게 피해를 주는 어둠 투사체를 발사합니다.";
                data.skillLevel = 1;
                data.coolDown = 1f;
                data.manaCost = 10;
                data.damage = 10f;
                data.range = 12f;
                data.castVisualScale = 0.25f;
                data.collisionLayers = LayerMask.GetMask("Enemy", "Wall", "Obstacle");
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.icon = projectileFrames[0];
            data.projectileFrames = projectileFrames;
            data.hitFrames = hitFrames;
            data.castFrames = castFrames;
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
            if (projectile == null) projectile = CreatePrefab(false, data);
            data.projectilePrefab = projectile.GetComponent<DarkboltProjectile>();
            GameObject skill = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPath);
            if (skill == null) skill = CreatePrefab(true, data);
            data.skillPrefab = skill;
            Save(data);

            SkillDatabaseSO database = Require<SkillDatabaseSO>(Root + "Skill/SkillDatabaseSO.asset");
            if (database.allSkills == null) database.allSkills = new List<SkillData>();
            if (!database.allSkills.Contains(data)) { database.allSkills.Add(data); Save(database); }

            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Relic of Darkbolt";
                relic.koreanName = "다크볼트 마법 책";
                relic.englishName = "Darkbolt Magic Book";
                relic.description_KR = "다크볼트 스킬을 얻는다.";
                relic.description_EN = "Grants the Darkbolt skill.";
                relic.Image = icon;
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
            Debug.Log("[DarkboltSkillSetup] 다크볼트 스킬과 유물 연결 완료.", data);
        }

        private static Sprite[] ConfigureFrames(string path, int size, int count)
        {
            Texture2D sheet = Require<Texture2D>(path);
            if (sheet.width != size * count || sheet.height != size)
                throw new InvalidOperationException("스프라이트 시트 크기가 예상과 다릅니다: " + path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            ConfigureImporter(importer, SpriteImportMode.Multiple, 32f);
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] old = provider.GetSpriteRects();
            SpriteRect[] rects = Enumerable.Range(0, count).Select(i =>
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path) + "_" + i;
                SpriteRect previous = old.FirstOrDefault(rect => rect.name == name) ?? old.FirstOrDefault(rect => rect.rect == new Rect(i * size, 0, size, size));
                return new SpriteRect { name = name, rect = new Rect(i * size, 0, size, size),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f),
                    spriteID = previous != null ? previous.spriteID : GUID.Generate() };
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
        }

        private static Sprite ConfigureIcon(string path)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new MissingReferenceException(path);
            ConfigureImporter(importer, SpriteImportMode.Single, 32f);
            importer.SaveAndReimport();
            return Require<Sprite>(path);
        }

        private static void ConfigureImporter(TextureImporter importer, SpriteImportMode mode, float ppu)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        private static GameObject CreatePrefab(bool skill, DarkboltSkillData data)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject(skill ? "DarkboltSkill" : "DarkboltProjectile");
                SceneManager.MoveGameObjectToScene(root, preview);
                if (skill) root.AddComponent<DarkboltSkill>().skillData = data;
                else
                {
                    root.AddComponent<DarkboltProjectile>();
                    SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                    renderer.sprite = data.projectileFrames[0];
                    renderer.sortingOrder = 10;
                    SpiritsGuidanceTrail trail = root.AddComponent<SpiritsGuidanceTrail>();
                    Sprite[] frames = ConfigureFrames(TailPath, 32, 5);
                    SerializedObject serializedTrail = new SerializedObject(trail);
                    SerializedProperty sprites = serializedTrail.FindProperty("trailSprites");
                    sprites.arraySize = frames.Length;
                    for (int i = 0; i < frames.Length; i++)
                        sprites.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                    serializedTrail.ApplyModifiedPropertiesWithoutUndo();
                    root.SetActive(false);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, skill ? SkillPath : ProjectilePath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new MissingReferenceException("필수 에셋이 없습니다: " + path);
        private static void Save(Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
    }
}
