using System.Linq;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ProjectileGateEntryEffectSetup
    {
        private const string ArtRoot = ProjectileGateSkillSetup.ArtRoot;
        private const string PrefabRoot = "Assets/Prefabs/Gameplay/Skills/";

        static ProjectileGateEntryEffectSetup() => EditorApplication.delayCall += SetupIfMissing;

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += SetupIfMissing;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ProjectileGateSkillData data = AssetDatabase.LoadAssetAtPath<ProjectileGateSkillData>(ProjectileGateSkillSetup.DataPath);
            if (data != null && data.outputProjectileMaterial == null) CreateOutputMaterial();
            if (data != null && data.outputEffectPrefab == null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/ProjectileGateExit.png") != null)
                CreateOutputEffect();
            if (data != null && (data.entryEffectPrefabs == null || data.entryEffectPrefabs.Length == 0) &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/ProjectileGateEntryA.png") != null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/ProjectileGateEntryB.png") != null)
                CreateAssets();
        }

        [MenuItem("Tools/Nytherion/Projectile Gate/Create Entry Effects")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            CreateOutputMaterial();
            CreateOutputEffect();
            ProjectileGateSkillData data = AssetDatabase.LoadAssetAtPath<ProjectileGateSkillData>(ProjectileGateSkillSetup.DataPath);
            if (data == null) throw new MissingReferenceException("분광 게이트 스킬 데이터가 없습니다.");
            data.entryEffectPrefabs = new[] { CreateEffect("ProjectileGateEntryA"), CreateEffect("ProjectileGateEntryB") };
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log("[ProjectileGateEntryEffectSetup] 일반 투사체 진입 애니메이션 2종 연결 완료.", data);
        }

        [MenuItem("Tools/Nytherion/Projectile Gate/Create Output Material")]
        public static void CreateOutputMaterial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ProjectileGateSkillData data = AssetDatabase.LoadAssetAtPath<ProjectileGateSkillData>(ProjectileGateSkillSetup.DataPath);
            if (data == null || data.outputProjectileMaterial != null) return;
            const string path = ArtRoot + "/ProjectileGateOutput.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Nytherion/Art/Common/Materials/SpriteFlash.shader");
                if (shader == null) throw new MissingReferenceException("투사체 색상 혼합 셰이더가 없습니다.");
                material = new Material(shader) { name = "ProjectileGateOutput" };
                material.SetColor("_FlashColor", new Color(1f, 0.16f, 0.1f, 1f));
                material.SetFloat("_FlashAmount", 0.8f);
                ConfigureOutputVisibility(material);
                AssetDatabase.CreateAsset(material, path);
            }
            data.outputProjectileMaterial = material;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        [MenuItem("Tools/Nytherion/Projectile Gate/Improve Output Visibility")]
        public static void ImproveOutputVisibility()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            CreateOutputMaterial();
            ProjectileGateSkillData data = AssetDatabase.LoadAssetAtPath<ProjectileGateSkillData>(ProjectileGateSkillSetup.DataPath);
            if (data == null || data.outputProjectileMaterial == null) return;
            ConfigureOutputVisibility(data.outputProjectileMaterial);
            EditorUtility.SetDirty(data.outputProjectileMaterial);
            AssetDatabase.SaveAssetIfDirty(data.outputProjectileMaterial);
        }

        private static void ConfigureOutputVisibility(Material material)
        {
            material.SetColor("_FlashColor", new Color(1f, 0.22f, 0.12f, 1f));
            material.SetFloat("_PreserveHighlights", 1f);
            material.SetColor("_HighlightColor", new Color(1f, 0.95f, 0.8f, 1f));
            material.SetFloat("_HighlightStrength", 0.8f);
            material.SetFloat("_OutlineEnabled", 1f);
            material.SetColor("_OutlineColor", new Color(1f, 0.72f, 0.35f, 1f));
            material.SetFloat("_OutlineWidth", 1f);
        }

        [MenuItem("Tools/Nytherion/Projectile Gate/Create Output Effect")]
        public static void CreateOutputEffect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ProjectileGateSkillData data = AssetDatabase.LoadAssetAtPath<ProjectileGateSkillData>(ProjectileGateSkillSetup.DataPath);
            if (data == null) throw new MissingReferenceException("분광 게이트 스킬 데이터가 없습니다.");
            data.outputEffectPrefab = CreateEffect("ProjectileGateExit", 32);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        private static ProjectileGateEntryEffect CreateEffect(string name, int frameSize = 64)
        {
            string sheetPath = ArtRoot + "/" + name + ".png";
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (sheet == null || sheet.width != frameSize * 4 || sheet.height != frameSize)
                throw new System.InvalidOperationException($"게이트 이펙트는 {frameSize}×{frameSize} 프레임 4장의 시트여야 합니다: " + sheetPath);
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(frameSize, frameSize), 64f);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] rects = provider.GetSpriteRects();
            foreach (SpriteRect rect in rects)
            {
                rect.alignment = SpriteAlignment.Custom;
                rect.pivot = new Vector2(0f, 0.5f);
            }
            provider.SetSpriteRects(rects);
            provider.Apply();
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();

            string clipPath = ArtRoot + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            clip.frameRate = 12f;
            clip.wrapMode = WrapMode.Once;
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding
            {
                path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite"
            }, keys);
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            clipSettings.startTime = 0f;
            clipSettings.stopTime = frames.Length / clip.frameRate;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            string controllerPath = ArtRoot + "/" + name + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddMotion(clip);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);
            }
            string prefabPath = PrefabRoot + name + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null) return prefab.GetComponent<ProjectileGateEntryEffect>();
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, preview);
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sortingOrder = 21;
                Animator animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ProjectileGateEntryEffect effect = root.AddComponent<ProjectileGateEntryEffect>();
                SerializedObject serialized = new SerializedObject(effect);
                serialized.FindProperty("animationClip").objectReferenceValue = clip;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath).GetComponent<ProjectileGateEntryEffect>();
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
