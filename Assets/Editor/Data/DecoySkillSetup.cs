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
    [InitializeOnLoad]
    public static class DecoySkillSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Skill/Decoy_Skill.asset";
        public const string DummyPath = "Assets/Prefabs/Gameplay/Skills/DecoyDummy.prefab";
        public const string SkillPath = "Assets/Prefabs/Gameplay/Skills/DecoySkill.prefab";
        public const string SheetPath = "Assets/Nytherion/Art/Skills/Sprites/Decoy/DecoySummon_Sheet.png";
        public const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Relic_Decoy.asset";
        private const string DataRoot = "Assets/Nytherion/Data/ScriptableObjects/";
        public const string Output = "output/decoy";

        static DecoySkillSetup() => EditorApplication.delayCall += SetupIfMissing;

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += SetupIfMissing; return; }
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                AssetDatabase.LoadAssetAtPath<DecoySkillData>(DataPath) == null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath) != null) CreateAssets();
        }

        [MenuItem("Tools/Nytherion/Decoy/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            try
            {
                Sprite[] frames = ConfigureFrames();
                DecoySkillData data = AssetDatabase.LoadAssetAtPath<DecoySkillData>(DataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<DecoySkillData>();
                    data.skillID = "skill_decoy";
                    data.skillType = SkillType.Decoy;
                    data.skillName = "허수아비";
                    data.description = "주변 적을 유인하는 허수아비를 소환합니다.";
                    data.skillLevel = 1;
                    data.coolDown = 45f;
                    data.range = 2.5f;
                    data.blockingLayers = LayerMask.GetMask("Wall", "Obstacle");
                    AssetDatabase.CreateAsset(data, DataPath);
                }
                data.summonFrames = frames;
                GameObject player = Require<GameObject>("Assets/Prefabs/Gameplay/Characters/Player/Player.prefab");
                data.dummySprite = player.GetComponent<SpriteRenderer>().sprite;
                data.icon = frames[0];
                GameObject dummy = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPath);
                if (dummy == null) dummy = CreatePrefab(false, data);
                data.dummyPrefab = dummy.GetComponent<DecoyDummy>();
                GameObject skill = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPath);
                if (skill == null) skill = CreatePrefab(true, data);
                data.skillPrefab = skill;
                Save(data);
                SkillDatabaseSO database = Require<SkillDatabaseSO>(DataRoot + "Skill/SkillDatabaseSO.asset");
                if (!database.allSkills.Contains(data)) { database.allSkills.Add(data); Save(database); }
                AddToPool(Require<GachaPoolSO>(DataRoot + "Gacha/GachaPool/Skill/SkillGachaPool.asset"), data, 1);
                RegisterRelic(data);
                File.WriteAllText(Output + "/setup.txt", $"PASS 더미 스킬·프리팹·{frames.Length}프레임·스킬 DB·가챠·각인 연결\n");
                Debug.Log("[DecoySkillSetup] 유인 더미 스킬 생성 완료.", data);
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        private static Sprite[] ConfigureFrames()
        {
            Texture2D texture = Require<Texture2D>(SheetPath);
            if (texture.width != 160 || texture.height != 64)
                throw new InvalidOperationException("더미 소환 시트는 32×64 프레임 5개여야 합니다.");
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 64f;
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
            var rects = new List<SpriteRect>();
            for (int i = 0; i < 5; i++)
            {
                string name = "DecoySummon_" + i.ToString("D2");
                SpriteRect existing = previous.FirstOrDefault(rect => rect.name == name);
                rects.Add(new SpriteRect { name = name, rect = new Rect(i * 32, 0, 32, 64),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f),
                    spriteID = existing != null ? existing.spriteID : GUID.Generate() });
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        }

        private static GameObject CreatePrefab(bool isSkill, DecoySkillData data)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject(isSkill ? "DecoySkill" : "DecoyDummy");
                SceneManager.MoveGameObjectToScene(root, preview);
                if (isSkill) root.AddComponent<DecoySkill>().skillData = data;
                else
                {
                    root.AddComponent<DecoyDummy>();
                    root.GetComponent<SpriteRenderer>().sprite = data.dummySprite;
                    root.GetComponent<SpriteRenderer>().color = new Color(0.65f, 0.9f, 1f, 1f);
                    root.GetComponent<SpriteRenderer>().sortingOrder = 2;
                    CircleCollider2D body = root.GetComponent<CircleCollider2D>();
                    body.radius = data.collisionRadius;
                    Rigidbody2D rigidbody = root.GetComponent<Rigidbody2D>();
                    rigidbody.bodyType = RigidbodyType2D.Kinematic;
                    rigidbody.useFullKinematicContacts = true;
                    rigidbody.gravityScale = 0f;
                    rigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
                    root.SetActive(false);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, isSkill ? SkillPath : DummyPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void RegisterRelic(DecoySkillData data)
        {
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Relic of Decoy";
                relic.koreanName = "허수아비 마법 책";
                RelicDescriptionSetup.GetSkillDescriptions(data, out relic.description_KR, out relic.description_EN);
                relic.Image = data.icon;
                relic.rarity = Rarity.Rare;
                relic.effectModules = new List<RelicEffectModule>
                { new RelicEffectModule { effects = new List<RelicEffectBase> { new GrantSkillEffect { skillData = data } } } };
                AssetDatabase.CreateAsset(relic, RelicPath);
            }
            RelicDatabaseSO database = Require<RelicDatabaseSO>(DataRoot + "Relics/RelicDatabase.asset");
            if (!database.allRelics.Contains(relic)) { database.allRelics.Add(relic); Save(database); }
            AddToPool(Require<GachaPoolSO>(DataRoot + "Gacha/GachaPool/Relic/Rare_Relic.asset"), relic, 100);
        }

        private static void AddToPool(GachaPoolSO pool, ScriptableObject item, int weight)
        {
            if (pool.items.Any(entry => entry.item == item)) return;
            pool.items.Add(new GachaItemRate { item = item, weight = weight });
            Save(pool);
        }
        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new MissingReferenceException("필수 에셋이 없습니다: " + path);
        private static void Save(Object asset)
        { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
    }
}
