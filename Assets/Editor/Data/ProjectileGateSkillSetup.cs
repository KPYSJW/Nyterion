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
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ProjectileGateSkillSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Skill/ProjectileGate_Skill.asset";
        public const string GatePath = "Assets/Prefabs/Gameplay/Skills/ProjectileGate.prefab";
        public const string SkillPath = "Assets/Prefabs/Gameplay/Skills/ProjectileGateSkill.prefab";
        public const string ArtRoot = "Assets/Nytherion/Art/Skills/Sprites/ProjectileGate";
        public const string SheetPath = ArtRoot + "/laser beam-On 8x3-Sheet.png";
        public const string ClipPath = ArtRoot + "/ProjectileGateBeam.anim";
        public const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Relic_ProjectileGate.asset";
        private const string DataRoot = "Assets/Nytherion/Data/ScriptableObjects/";

        static ProjectileGateSkillSetup() => EditorApplication.delayCall += SetupIfMissing;

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += SetupIfMissing;
                return;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                AssetDatabase.LoadAssetAtPath<ProjectileGateSkillData>(DataPath) == null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath) != null)
                CreateAssets();
        }

        [MenuItem("Tools/Nytherion/Projectile Gate/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Sprite[] frames = ConfigureFrames();
            TextureImporter endpointImporter = RequireImporter(ArtRoot + "/Sprite-0002.png");
            ConfigureImporter(endpointImporter, SpriteImportMode.Single, 16f);
            endpointImporter.SaveAndReimport();
            Sprite endpoint = Require<Sprite>(ArtRoot + "/Sprite-0002.png");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "ProjectileGateBeam", frameRate = 12f };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i % frames.Length] };
            AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding
            {
                path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite"
            }, keys);
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            Save(clip);
            const string controllerPath = ArtRoot + "/ProjectileGateBeam.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddMotion(clip);
                Save(controller);
            }
            ProjectileGateSkillData data = AssetDatabase.LoadAssetAtPath<ProjectileGateSkillData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ProjectileGateSkillData>();
                data.ApplyCreationDefaults();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            if (data.icon == null) data.icon = endpoint;
            if (data.beamAnimation == null) data.beamAnimation = clip;
            GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePath);
            if (gatePrefab == null) gatePrefab = CreateGatePrefab(endpoint, frames[0], controller);
            if (data.gatePrefab == null) data.gatePrefab = gatePrefab.GetComponent<ProjectileGateController>();
            GameObject skillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPath);
            if (skillPrefab == null)
            {
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject("ProjectileGateSkill");
                    SceneManager.MoveGameObjectToScene(root, preview);
                    root.AddComponent<ProjectileGateSkill>().skillData = data;
                    skillPrefab = PrefabUtility.SaveAsPrefabAsset(root, SkillPath);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            if (data.skillPrefab == null) data.skillPrefab = skillPrefab;
            Save(data);
            SkillDatabaseSO database = Require<SkillDatabaseSO>(DataRoot + "Skill/SkillDatabaseSO.asset");
            if (database.allSkills == null) database.allSkills = new List<SkillData>();
            if (!database.allSkills.Contains(data)) { database.allSkills.Add(data); Save(database); }
            AddToPool(Require<GachaPoolSO>(DataRoot + "Gacha/GachaPool/Skill/SkillGachaPool.asset"), data, 1);
            RegisterRelic(data);
            Debug.Log($"[ProjectileGateSkillSetup] 분광 게이트와 88×32 애니메이션 {frames.Length}프레임, 스킬·각인 획득 경로 연결 완료.", data);
        }

        private static Sprite[] ConfigureFrames()
        {
            Texture2D texture = Require<Texture2D>(SheetPath);
            if (texture.width % 88 != 0 || texture.height % 32 != 0)
                throw new InvalidOperationException("레이저 시트의 각 프레임은 88×32여야 합니다.");
            TextureImporter importer = RequireImporter(SheetPath);
            ConfigureImporter(importer, SpriteImportMode.Multiple, 32f);
            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] previous = provider.GetSpriteRects();
            var rects = new List<SpriteRect>();
            int index = 0;
            // 원본 시트의 위쪽 행부터 왼쪽에서 오른쪽 순서로 재생합니다.
            for (int y = texture.height - 32; y >= 0; y -= 32)
                for (int x = 0; x < texture.width; x += 88)
                {
                    string name = "ProjectileGateBeam_" + (index++).ToString("D2");
                    SpriteRect existing = previous.FirstOrDefault(rect => rect.name == name);
                    rects.Add(new SpriteRect
                    {
                        name = name, rect = new Rect(x, y, 88, 32), alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f), spriteID = existing != null ? existing.spriteID : GUID.Generate()
                    });
                }
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        }

        private static GameObject CreateGatePrefab(Sprite endpoint, Sprite beam, AnimatorController controller)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("ProjectileGate");
                SceneManager.MoveGameObjectToScene(root, preview);
                ProjectileGateController gate = root.AddComponent<ProjectileGateController>();
                root.GetComponent<BoxCollider2D>().isTrigger = true;
                SpriteRenderer left = CreateRenderer(root, "LeftEndpoint", endpoint, 21);
                left.flipX = true;
                SpriteRenderer right = CreateRenderer(root, "RightEndpoint", endpoint, 21);
                SpriteRenderer laser = CreateRenderer(root, "Beam", beam, 20);
                Animator animator = laser.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                SerializedObject serialized = new SerializedObject(gate);
                serialized.FindProperty("leftEndpoint").objectReferenceValue = left;
                serialized.FindProperty("rightEndpoint").objectReferenceValue = right;
                serialized.FindProperty("beamVisual").objectReferenceValue = laser.transform;
                serialized.FindProperty("beamAnimator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, GatePath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static SpriteRenderer CreateRenderer(GameObject root, string name, Sprite sprite, int order)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void RegisterRelic(ProjectileGateSkillData data)
        {
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Relic of Projectile Gate";
                relic.koreanName = "분광 게이트 각인";
                RelicDescriptionSetup.GetSkillDescriptions(data, out relic.description_KR, out relic.description_EN);
                relic.Image = data.icon;
                relic.rarity = Rarity.Rare;
                relic.effectModules = new List<RelicEffectModule>
                {
                    new RelicEffectModule { effects = new List<RelicEffectBase> { new GrantSkillEffect { skillData = data } } }
                };
                AssetDatabase.CreateAsset(relic, RelicPath);
            }
            RelicDatabaseSO database = Require<RelicDatabaseSO>(DataRoot + "Relics/RelicDatabase.asset");
            if (database.allRelics == null) database.allRelics = new List<RelicData>();
            if (!database.allRelics.Contains(relic)) { database.allRelics.Add(relic); Save(database); }
            AddToPool(Require<GachaPoolSO>(DataRoot + "Gacha/GachaPool/Relic/Rare_Relic.asset"), relic, 100);
        }

        private static void AddToPool(GachaPoolSO pool, ScriptableObject item, int weight)
        {
            if (pool.items == null) pool.items = new List<GachaItemRate>();
            if (pool.items.Any(entry => entry.item == item)) return;
            pool.items.Add(new GachaItemRate { item = item, weight = weight });
            Save(pool);
        }

        private static TextureImporter RequireImporter(string path) => AssetImporter.GetAtPath(path) as TextureImporter ??
            throw new MissingReferenceException("스프라이트 원본이 없습니다: " + path);

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

        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new MissingReferenceException("필수 에셋이 없습니다: " + path);

        private static void Save(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
