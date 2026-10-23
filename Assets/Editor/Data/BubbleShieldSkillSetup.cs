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
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class BubbleShieldSkillSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Skill/BubbleShield_Skill.asset";
        public const string PrefabPath = "Assets/Prefabs/Gameplay/Skills/BubbleShieldSkill.prefab";
        public const string ArtRoot = "Assets/Nytherion/Art/Skills/Sprites/BubbleShield/";
        public const string Output = "output/bubble-shield";
        public const string HitPrefabPath = "Assets/Prefabs/Gameplay/Skills/BubbleShieldHit.prefab";
        public const string ShieldBarPath = "Assets/Nytherion/Art/UI/HUD/ShieldBarFill.png";
        private const string DataRoot = "Assets/Nytherion/Data/ScriptableObjects/";

        static BubbleShieldSkillSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/bar-setup.request"))
            {
                File.Delete(Output + "/bar-setup.request");
                try { CreateShieldBar(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/bar-setup.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (File.Exists(Output + "/hit-setup.request"))
            {
                File.Delete(Output + "/hit-setup.request");
                try { CreateHitEffect(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/hit-setup.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (!File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { CreateAssets(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Bubble Shield/스킬 에셋 생성 및 연결")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            Sprite[] summon = ImportFrames("BubbleShieldSummon", 7);
            Sprite[] crash = ImportFrames("BubbleShieldCrash", 4);
            Sprite[] maintained = ImportFrames("BubbleShield", 1);
            BubbleShieldSkillData data = AssetDatabase.LoadAssetAtPath<BubbleShieldSkillData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BubbleShieldSkillData>();
                data.skillID = "skill_bubble_shield";
                data.skillType = SkillType.BubbleShield;
                data.skillName = "비눗방울";
                data.description = "피해를 흡수하는 비눗방울을 생성합니다.";
                data.skillLevel = 1;
                data.coolDown = 20f;
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.summonFrames = summon;
            data.crashFrames = crash;
            data.shieldSprite = maintained[0];
            data.icon = maintained[0];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject("BubbleShieldSkill");
                    SceneManager.MoveGameObjectToScene(root, preview);
                    root.AddComponent<BubbleShieldSkill>().skillData = data;
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            data.skillPrefab = prefab;
            Save(data);
            SkillDatabaseSO skills = Require<SkillDatabaseSO>(DataRoot + "Skill/SkillDatabaseSO.asset");
            if (!skills.allSkills.Contains(data)) { skills.allSkills.Add(data); Save(skills); }
            AddToPool(DataRoot + "Gacha/GachaPool/Skill/SkillGachaPool.asset", data, 1);
            RegisterRelic(data);
            RegisterLocalization(data);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(ShieldBarPath) != null) CreateShieldBar();
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "BubbleShieldHit.png") != null) CreateHitEffect();
            File.WriteAllText(Output + "/setup.txt", "PASS 실드 데이터·프리팹·생성 7/유지 1/파괴 4프레임·DB·가챠·각인 연결\n");
            Debug.Log("[BubbleShieldSkillSetup] 버블 실드 스킬 연결 완료.", data);
        }

        [MenuItem("Tools/Nytherion/Bubble Shield/실드 바 이미지 연결")]
        public static void CreateShieldBar()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            BubbleShieldSkillData data = Require<BubbleShieldSkillData>(DataPath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(ShieldBarPath);
            if (importer == null) throw new MissingReferenceException("실드 바 이미지가 없습니다.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            data.shieldBarSprite = Require<Sprite>(ShieldBarPath);
            Save(data);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/bar-setup.txt", "PASS 첨부 실드 바 스프라이트 연결\n");
        }

        [MenuItem("Tools/Nytherion/Bubble Shield/피격 연출 생성 및 연결")]
        public static void CreateHitEffect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            BubbleShieldSkillData data = Require<BubbleShieldSkillData>(DataPath);
            Sprite[] frames = ImportFrames("BubbleShieldHit", 3, 16);
            string clipPath = ArtRoot + "BubbleShieldHit.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "BubbleShieldHit", frameRate = 12f };
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / clip.frameRate, value = null };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            Save(clip);
            string controllerPath = ArtRoot + "BubbleShieldHit.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddMotion(clip);
                Save(controller);
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitPrefabPath);
            if (prefab == null)
            {
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject("BubbleShieldHit");
                    SceneManager.MoveGameObjectToScene(root, preview);
                    root.AddComponent<SpriteRenderer>().sprite = frames[0];
                    Animator animator = root.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    BubbleShieldHitEffect effect = root.AddComponent<BubbleShieldHitEffect>();
                    SerializedObject serialized = new SerializedObject(effect);
                    serialized.FindProperty("animationClip").objectReferenceValue = clip;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    root.SetActive(false);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, HitPrefabPath);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            data.hitEffectPrefab = prefab.GetComponent<BubbleShieldHitEffect>();
            Save(data);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/hit-setup.txt", "PASS 16×16 피격 3프레임·일회 재생 클립·재사용 프리팹 연결\n");
        }

        private static Sprite[] ImportFrames(string name, int count, int cellSize = 32)
        {
            string path = ArtRoot + name + ".png";
            Texture2D texture = Require<Texture2D>(path);
            if (texture.width != count * cellSize || texture.height != cellSize)
                throw new InvalidOperationException(name + $"의 프레임은 {cellSize}×{cellSize} 크기여야 합니다.");
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
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
            var rects = new List<SpriteRect>();
            for (int i = 0; i < count; i++)
            {
                string frameName = name + "_" + i.ToString("D2");
                SpriteRect existing = previous.FirstOrDefault(rect => rect.name == frameName);
                rects.Add(new SpriteRect { name = frameName, rect = new Rect(i * cellSize, 0, cellSize, cellSize),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f),
                    spriteID = existing != null ? existing.spriteID : GUID.Generate() });
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        }

        private static void RegisterRelic(BubbleShieldSkillData data)
        {
            const string path = DataRoot + "Relics/SkillRelics/Relic_BubbleShield.asset";
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(path);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Relic of Bubble Shield";
                relic.koreanName = "비눗방울";
                RelicDescriptionSetup.GetSkillDescriptions(data, out relic.description_KR, out relic.description_EN);
                relic.Image = data.icon;
                relic.rarity = Rarity.Rare;
                relic.effectModules = new List<RelicEffectModule>
                { new RelicEffectModule { effects = new List<RelicEffectBase> { new GrantSkillEffect { skillData = data } } } };
                AssetDatabase.CreateAsset(relic, path);
            }
            RelicDatabaseSO relics = Require<RelicDatabaseSO>(DataRoot + "Relics/RelicDatabase.asset");
            if (!relics.allRelics.Contains(relic)) { relics.allRelics.Add(relic); Save(relics); }
            AddToPool(DataRoot + "Gacha/GachaPool/Relic/Rare_Relic.asset", relic, 100);
        }

        private static void RegisterLocalization(BubbleShieldSkillData data)
        {
            SetTranslation(LocalizationTables.Skills, LocalizationKeys.SkillName(data.skillID), "비눗방울", "Bubble Shield");
            SetTranslation(LocalizationTables.Skills, LocalizationKeys.SkillDescription(data.skillID), data.description,
                "Creates a damage-absorbing shield.");
            SetTranslation(LocalizationTables.UI, "ui.skill_tooltip.bubble_shield_stats",
                "실드량  {0}\n지속시간  {1}초\n쿨타임  {2}초", "Shield  {0}\nDuration  {1}s\nCooldown  {2}s", true);
            const string relicName = "Relic of Bubble Shield";
            SetTranslation(LocalizationTables.Relics, LocalizationKeys.RelicName(relicName), "버블 실드 각인", "Bubble Shield Inscription");
            SetTranslation(LocalizationTables.Relics, LocalizationKeys.RelicDescription(relicName),
                "피해를 흡수하는 버블 실드 스킬 획득", "Grants a bubble shield skill that absorbs damage.");
        }

        private static void SetTranslation(string collectionName, string key, string korean, string english, bool smart = false)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
            if (collection == null) return;
            foreach (StringTable table in collection.StringTables)
            {
                string code = table.LocaleIdentifier.Code;
                if (!code.StartsWith("ko", StringComparison.OrdinalIgnoreCase) &&
                    !code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) continue;
                table.AddEntry(key, code.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? korean : english).IsSmart = smart;
                Save(table);
            }
            Save(collection.SharedData);
        }

        private static void AddToPool(string path, ScriptableObject item, int weight)
        {
            GachaPoolSO pool = Require<GachaPoolSO>(path);
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
