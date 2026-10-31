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
    [InitializeOnLoad]
    public static class IceShotSkillSetup
    {
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/";
        private const string DataPath = Root + "Skill/IceShot_Skill.asset";
        private const string RelicPath = Root + "Relics/SkillRelics/Relic_IceShot.asset";
        private const string SkillPath = "Assets/Prefabs/Gameplay/Skills/IceShotSkill.prefab";
        private const string ProjectilePath = "Assets/Prefabs/Gameplay/Skills/IceShotProjectile.prefab";
        private const string Sprites = "Assets/Nytherion/Art/Skills/Sprites/IceShot_";

        static IceShotSkillSetup()
        {
            EditorApplication.delayCall += SetupIfMissing;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += SetupIfMissing;
            };
        }

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += SetupIfMissing; return; }
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                AssetDatabase.LoadAssetAtPath<IceShotSkillData>(DataPath) == null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(Sprites + "Projectile.png") != null)
                CreateAssets();
            IceShotSkillData data = AssetDatabase.LoadAssetAtPath<IceShotSkillData>(DataPath);
            if (!EditorApplication.isPlayingOrWillChangePlaymode && data != null &&
                (data.hitFrames == null || data.hitFrames.Length == 0 || data.hitFrames.Any(frame => frame == null)) &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(Sprites + "Hit.png") != null)
                UpdateHitAnimation();
        }

        [MenuItem("Tools/Nytherion/IceShot/Update Hit Animation")]
        public static void UpdateHitAnimation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            IceShotSkillData data = Require<IceShotSkillData>(DataPath);
            data.hitFrames = ConfigureHitFrames();
            Save(data);
            Debug.Log("[IceShotSkillSetup] 타격 이펙트 3프레임 연결 완료.", data);
        }

        [MenuItem("Tools/Nytherion/IceShot/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Sprite[] projectileFrames = ConfigureFrames(Sprites + "Projectile.png", 32, 9);
            Sprite icon = ConfigureIcon(Sprites + "Icon.png");
            IceShotSkillData data = AssetDatabase.LoadAssetAtPath<IceShotSkillData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<IceShotSkillData>();
                data.skillID = "skill_ice_shot";
                data.skillType = SkillType.IceShot;
                data.skillName = "아이스샷";
                data.description = "적에게 피해를 주고 빙결을 부여하는 얼음 투사체를 발사합니다.";
                data.skillLevel = 1;
                data.coolDown = 1f;
                data.manaCost = 10;
                data.damage = 10f;
                data.range = 12f;
                data.collisionLayers = LayerMask.GetMask("Enemy", "Wall", "Obstacle");
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.icon = icon;
            data.projectileFrames = projectileFrames;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(Sprites + "Hit.png") != null) data.hitFrames = ConfigureHitFrames();
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
            if (projectile == null) projectile = CreatePrefab(false, data);
            data.projectilePrefab = projectile.GetComponent<IceShotProjectile>();
            GameObject skill = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPath);
            if (skill == null) skill = CreatePrefab(true, data);
            data.skillPrefab = skill;
            Save(data);
            SkillDatabaseSO database = Require<SkillDatabaseSO>(Root + "Skill/SkillDatabaseSO.asset");
            if (!database.allSkills.Contains(data)) { database.allSkills.Add(data); Save(database); }

            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Relic of Ice Shot";
                relic.koreanName = "아이스샷 마법 책";
                relic.englishName = "Ice Shot Magic Book";
                relic.description_KR = "적에게 피해와 빙결을 주는 아이스샷 스킬을 얻는다.";
                relic.description_EN = "Grants an ice shot skill that damages and chills enemies.";
                relic.Image = icon;
                relic.rarity = Rarity.Common;
                relic.effectModules = new List<RelicEffectModule>
                { new RelicEffectModule { effects = new List<RelicEffectBase> { new GrantSkillEffect { skillData = data } } } };
                AssetDatabase.CreateAsset(relic, RelicPath);
            }
            RelicInfluencePolicy.Normalize(relic);
            Save(relic);
            RelicDatabaseSO relicDatabase = Require<RelicDatabaseSO>(Root + "Relics/RelicDatabase.asset");
            if (!relicDatabase.allRelics.Contains(relic)) { relicDatabase.allRelics.Add(relic); Save(relicDatabase); }
            GachaPoolSO pool = Require<GachaPoolSO>(Root + "Gacha/GachaPool/Relic/Common_Relic.asset");
            if (!pool.items.Any(entry => entry.item == relic))
            { pool.items.Add(new GachaItemRate { item = relic, weight = 100 }); Save(pool); }
            Debug.Log("[IceShotSkillSetup] 아이스샷 스킬과 유물 연결 완료.", data);
        }

        private static Sprite[] ConfigureFrames(string path, int size, int count)
        {
            Texture2D sheet = Require<Texture2D>(path);
            if (sheet.width != size * 3 || sheet.height != size * 3 || count != 9)
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
                SpriteRect previous = old.FirstOrDefault(rect => rect.name == name) ?? old.FirstOrDefault(rect => rect.rect == new Rect((i % 3) * size, (2 - i / 3) * size, size, size));
                return new SpriteRect { name = name, rect = new Rect((i % 3) * size, (2 - i / 3) * size, size, size),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f),
                    spriteID = previous != null ? previous.spriteID : GUID.Generate() };
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderByDescending(sprite => sprite.rect.y).ThenBy(sprite => sprite.rect.x).ToArray();
        }

        private static Sprite[] ConfigureHitFrames()
        {
            string path = Sprites + "Hit.png";
            Texture2D sheet = Require<Texture2D>(path);
            if (sheet.width != 192 || sheet.height != 64)
                throw new InvalidOperationException("타격 이펙트는 64×64 프레임 3개여야 합니다.");
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            ConfigureImporter(importer, SpriteImportMode.Multiple, 32f);
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] old = provider.GetSpriteRects();
            // 각 프레임의 보이는 가장 아래 픽셀을 충돌점에 맞춥니다.
            float[] bottomPadding = { 6f, 2f, 0f };
            SpriteRect[] rects = Enumerable.Range(0, 3).Select(i =>
            {
                string name = "IceShot_Hit_" + i;
                SpriteRect previous = old.FirstOrDefault(rect => rect.name == name);
                return new SpriteRect { name = name, rect = new Rect(i * 64, 0, 64, 64),
                    alignment = SpriteAlignment.Custom, pivot = new Vector2(0.5f, bottomPadding[i] / 64f),
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

        private static GameObject CreatePrefab(bool skill, IceShotSkillData data)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject(skill ? "IceShotSkill" : "IceShotProjectile");
                SceneManager.MoveGameObjectToScene(root, preview);
                if (skill) root.AddComponent<IceShotSkill>().skillData = data;
                else
                {
                    root.AddComponent<IceShotProjectile>();
                    SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                    renderer.sprite = data.projectileFrames[0];
                    renderer.sortingOrder = 10;
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
