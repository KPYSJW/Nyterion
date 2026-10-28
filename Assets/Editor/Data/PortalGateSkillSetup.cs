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
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class PortalGateSkillSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Skill/PortalGate_Skill.asset";
        public const string PortalPath = "Assets/Prefabs/Gameplay/Skills/PortalGate.prefab";
        public const string SkillPath = "Assets/Prefabs/Gameplay/Skills/PortalGateSkill.prefab";
        public const string SheetPath = "Assets/Nytherion/Art/Skills/Sprites/PortalGate/portal_gate_blue_32.png";
        public const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Relic_PortalGate.asset";
        public const string Description = "현재 위치와 마우스 방향의 카메라 화면 가장자리에 연결된 포탈을 생성합니다. 벽이 있으면 벽 앞에 생성합니다.\n포탈 위에서 [F]로 왕복합니다. 두 포탈은 지속시간과 이용 횟수를 공유합니다.";
        private const string DataRoot = "Assets/Nytherion/Data/ScriptableObjects/";
        private const string Output = "output/portal-gate";

        static PortalGateSkillSetup() => EditorApplication.delayCall += SetupIfMissing;

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += SetupIfMissing;
                return;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                AssetDatabase.LoadAssetAtPath<PortalGateSkillData>(DataPath) == null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath) != null) CreateAssets();
        }

        [MenuItem("Tools/Nytherion/Portal Gate/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            try
            {
                Sprite[] frames = ConfigureFrames();
                PortalGateSkillData data = AssetDatabase.LoadAssetAtPath<PortalGateSkillData>(DataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<PortalGateSkillData>();
                    data.skillID = "skill_portal_gate";
                    data.skillType = SkillType.PortalGate;
                    data.skillName = "차원문";
                    data.description = Description;
                    data.skillLevel = 1;
                    data.coolDown = 45f;
                    data.blockingLayers = LayerMask.GetMask("Wall", "Obstacle");
                    AssetDatabase.CreateAsset(data, DataPath);
                }
                data.animationFrames = frames;
                data.icon = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
                    .FirstOrDefault(sprite => sprite.name == "PortalGateIcon") ?? frames[0];
                GameObject portal = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPath);
                if (portal == null) portal = CreatePortalPrefab(frames[0]);
                data.portalPrefab = portal.GetComponent<PortalGateEndpoint>();
                GameObject skillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPath);
                if (skillPrefab == null)
                {
                    Scene preview = EditorSceneManager.NewPreviewScene();
                    try
                    {
                        GameObject root = new GameObject("PortalGateSkill");
                        SceneManager.MoveGameObjectToScene(root, preview);
                        root.AddComponent<PortalGateSkill>().skillData = data;
                        skillPrefab = PrefabUtility.SaveAsPrefabAsset(root, SkillPath);
                    }
                    finally { EditorSceneManager.ClosePreviewScene(preview); }
                }
                data.skillPrefab = skillPrefab;
                Save(data);
                SkillDatabaseSO database = Require<SkillDatabaseSO>(DataRoot + "Skill/SkillDatabaseSO.asset");
                if (database.allSkills == null) database.allSkills = new List<SkillData>();
                if (!database.allSkills.Contains(data)) { database.allSkills.Add(data); Save(database); }
                AddToPool(Require<GachaPoolSO>(DataRoot + "Gacha/GachaPool/Skill/SkillGachaPool.asset"), data, 1);
                RegisterRelic(data);
                File.WriteAllText(Output + "/setup.txt", $"PASS 차원문 스킬·포탈 프리팹·{frames.Length}프레임·스킬 DB·가챠·각인 연결\n");
                Debug.Log("[PortalGateSkillSetup] 차원문 스킬 에셋 생성 완료.", data);
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Portal Gate/Update Camera Placement Data")]
        public static void UpdateCameraPlacementAssets()
        {
            PortalGateSkillData data = Require<PortalGateSkillData>(DataPath);
            data.description = Description;
            data.range = 0f;
            Save(data);
            RelicData relic = Require<RelicData>(RelicPath);
            RelicDescriptionSetup.GetSkillDescriptions(data, out relic.description_KR, out relic.description_EN);
            Save(relic);
        }

        private static Sprite[] ConfigureFrames()
        {
            Texture2D texture = Require<Texture2D>(SheetPath);
            if (texture.width % 32 != 0 || texture.height != 32)
                throw new InvalidOperationException("포탈 시트는 가로로 나열된 32×32 프레임이어야 합니다.");
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24f;
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
            for (int x = 0; x < texture.width; x += 32)
            {
                string name = "PortalGate_" + (x / 32).ToString("D2");
                SpriteRect existing = previous.FirstOrDefault(rect => rect.name == name);
                rects.Add(new SpriteRect
                {
                    name = name, rect = new Rect(x, 0, 32, 32), alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f), spriteID = existing != null ? existing.spriteID : GUID.Generate()
                });
            }
            rects.AddRange(previous.Where(rect => rect.name == "PortalGateIcon"));
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
                .Where(sprite => sprite.name.StartsWith("PortalGate_")).OrderBy(sprite => sprite.name).ToArray();
        }

        private static GameObject CreatePortalPrefab(Sprite sprite)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("PortalGate");
                SceneManager.MoveGameObjectToScene(root, preview);
                root.layer = LayerMask.NameToLayer("Interactable");
                root.AddComponent<PortalGateEndpoint>();
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = -2;
                CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.6f;

                GameObject label = new GameObject("InteractionPrompt");
                label.transform.SetParent(root.transform, false);
                label.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                TextMeshPro text = label.AddComponent<TextMeshPro>();
                text.text = string.Empty;
                text.font = Require<TMP_FontAsset>("Assets/Nytherion/Art/UI/Fonts/Galmuri/Galmuri11 SDF.asset");
                text.fontSize = 2.1f;
                text.alignment = TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(6f, 0.6f);
                text.enableWordWrapping = false;
                text.color = new Color(0.75f, 0.92f, 1f, 1f);
                text.GetComponent<MeshRenderer>().sortingOrder = 30;
                label.SetActive(false);
                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, PortalPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void RegisterRelic(PortalGateSkillData data)
        {
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Relic of Portal Gate";
                relic.koreanName = "차원문";
                RelicDescriptionSetup.GetSkillDescriptions(data, out relic.description_KR, out relic.description_EN);
                relic.Image = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Relics/Sprites/WarpGear.png") ?? data.icon;
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

        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new MissingReferenceException("필수 에셋이 없습니다: " + path);

        private static void Save(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
