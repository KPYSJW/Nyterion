using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ThermalShockEffectSetup
    {
        private const string Output = "output/thermal-shock";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/ThermalShockEffect.png";
        private const string AnimationRoot = "Assets/Nytherion/Art/Combat/VFX/Animations";
        private const string ClipPath = AnimationRoot + "/ThermalShockExplosion.anim";
        private const string ControllerPath = AnimationRoot + "/ThermalShockExplosion.controller";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/ThermalShockExplosionVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/ThermalShockTranscendence.asset";

        static ThermalShockEffectSetup() => EditorApplication.update += ProcessRequest;

        private static void ProcessRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/유물/열충격 폭발 애니메이션 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            RelicTranscendenceData data = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(DataPath);
            ThermalShockEffect[] effects = data != null ? data.effectModules.Where(module => module != null && module.effects != null)
                .SelectMany(module => module.effects).OfType<ThermalShockEffect>().ToArray() : Array.Empty<ThermalShockEffect>();
            if (effects.Length != 1) throw new InvalidOperationException("열충격 초월 효과 모듈을 확인해 주세요.");
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (sheet == null || sheet.width != 576 || sheet.height != 64)
                throw new InvalidOperationException("열충격은 64×64 프레임 9장의 가로 시트여야 합니다.");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 64f);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            if (frames.Length != 9) throw new InvalidOperationException("열충격 시트의 슬라이스 수가 올바르지 않습니다.");

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "ThermalShockExplosion" };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            clip.frameRate = 12f;
            clip.wrapMode = WrapMode.Once;
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            clipSettings.startTime = 0f;
            clipSettings.stopTime = frames.Length / clip.frameRate;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Select(entry => entry.state).FirstOrDefault(entry => entry.name == "Explosion");
            if (state == null) state = machine.AddState("Explosion");
            state.motion = clip;
            state.speed = 1f;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && existing.GetComponent<ThermalShockExplosionVFX>() == null)
                throw new InvalidOperationException("같은 경로에 다른 용도의 프리팹이 있습니다.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            ThermalShockExplosionVFX prefab;
            GameObject root = null;
            try
            {
                root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("ThermalShockExplosionVFX");
                if (existing == null) SceneManager.MoveGameObjectToScene(root, preview);
                ThermalShockExplosionVFX effect = root.GetComponent<ThermalShockExplosionVFX>() ?? root.AddComponent<ThermalShockExplosionVFX>();
                SerializedObject serialized = new SerializedObject(effect);
                serialized.FindProperty("animationClip").objectReferenceValue = clip;
                serialized.FindProperty("nativeRadius").floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sortingOrder = 100;
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                Animator animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.enabled = false;
                root.SetActive(true);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<ThermalShockExplosionVFX>();
            }
            finally
            {
                if (existing != null && root != null) PrefabUtility.UnloadPrefabContents(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }

            effects[0].explosionEffectPrefab = prefab;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Verify();
            Debug.Log("[ThermalShockEffectSetup] 열충격 폭발 애니메이션과 초월 효과를 연결했습니다.", data);
        }

        public static void Verify()
        {
            RelicTranscendenceData data = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(DataPath);
            ThermalShockEffect effect = data.effectModules.SelectMany(module => module.effects).OfType<ThermalShockEffect>().Single();
            ThermalShockExplosionVFX prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<ThermalShockExplosionVFX>();
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
            if (effect.explosionEffectPrefab != prefab || frames.Length != 9 || keys.Length != 9 ||
                keys.Where((key, index) => key.value != frames[index]).Any() ||
                AnimationUtility.GetAnimationClipSettings(clip).loopTime || Mathf.Abs(clip.length - 0.75f) > 0.001f ||
                prefab.GetComponent<Animator>().runtimeAnimatorController == null || prefab.GetComponent<Collider2D>() != null)
                throw new InvalidOperationException("열충격 애니메이션·프리팹·효과 참조 검증에 실패했습니다.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/verification.txt",
                "PASS 열충격 64×64 프레임 9장, 12fps, 0.75초, 반복 없음\n" +
                "PASS 폭발 프리팹·Animator·초월 효과 참조, 추가 충돌체 없음\n" +
                $"설정: 피해 배율={effect.damageRatio}, 기본 폭발 반경={effect.explosionRadius}, 최대 대상={effect.maxTargets}, 직접 타격당 1회\n");
        }
    }
}
