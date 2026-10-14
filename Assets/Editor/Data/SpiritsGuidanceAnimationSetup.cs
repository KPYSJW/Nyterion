using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class SpiritsGuidanceAnimationSetup
    {
        private const string Output = "output/homing-magic";
        private const string Folder = "Assets/Nytherion/Art/Combat/VFX/Animations/Homing";

        static SpiritsGuidanceAnimationSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/start-effects.request"))
            {
                File.Delete(Output + "/start-effects.request");
                try { ApplyStartEffects(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/start-effects.txt", error.ToString());
                    Debug.LogException(error);
                }
            }
            if (!File.Exists(Output + "/variants.request")) return;
            File.Delete(Output + "/variants.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/variants.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Homing Magic/Apply Three Start Effects")]
        public static void ApplyStartEffects()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(HomingMagicSetup.DataPath);
            if (data == null || data.projectileAnimationVariants == null || data.projectileAnimationVariants.Length != 3)
                throw new InvalidOperationException("정령의 인도의 3종 투사체 연결을 확인해 주세요.");
            var prefabs = new GameObject[3];
            for (int i = 0; i < prefabs.Length; i++)
            {
                string name = "SpiritsGuidanceStart" + (i + 1);
                string source = "Assets/Nytherion/Art/Combat/VFX/Sprites/Spirits'GuidanceProjStart" + (i == 0 ? "" : i.ToString()) + ".png";
                Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Sprite>()
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
                if (frames.Length != 4) throw new InvalidOperationException("시작 이펙트의 4프레임을 확인해 주세요: " + source);
                string clipPath = Folder + "/" + name + ".anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip { name = name };
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                clip.ClearCurves();
                clip.frameRate = 18f;
                // 마지막 프레임도 한 프레임 동안 보여준 뒤 발사합니다.
                var keys = new ObjectReferenceKeyframe[frames.Length + 1];
                for (int frame = 0; frame < keys.Length; frame++)
                    keys[frame] = new ObjectReferenceKeyframe { time = frame / clip.frameRate, value = frames[Mathf.Min(frame, frames.Length - 1)] };
                AnimationUtility.SetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
                var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
                clipSettings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
                AnimationUtility.SetAnimationEvents(clip, new[]
                {
                    new AnimationEvent { time = clip.length, functionName = nameof(ProjectileStartEffect.OnAnimationComplete) }
                });
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                string controllerPath = Folder + "/" + name + ".controller";
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                AnimatorState state = machine.states.FirstOrDefault(child => child.state.name == name).state;
                if (state == null) state = machine.AddState(name);
                state.motion = clip;
                machine.defaultState = state;
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);
                string prefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/" + name + ".prefab";
                bool existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
                GameObject root = existingPrefab
                    ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(name);
                try
                {
                    root.transform.localScale = Vector3.one * 0.8f;
                    SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                    if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                    renderer.sprite = frames[0];
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<GameObject>(HomingMagicSetup.ProjectilePath).GetComponent<SpriteRenderer>().sharedMaterial;
                    Animator animator = root.GetComponent<Animator>();
                    if (animator == null) animator = root.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.keepAnimatorStateOnDisable = false;
                    ProjectileStartEffect effect = root.GetComponent<ProjectileStartEffect>();
                    if (effect == null) effect = root.AddComponent<ProjectileStartEffect>();
                    var serialized = new SerializedObject(effect);
                    serialized.FindProperty("animationClip").objectReferenceValue = clip;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    prefabs[i] = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    if (existingPrefab) PrefabUtility.UnloadPrefabContents(root);
                    else UnityEngine.Object.DestroyImmediate(root);
                }
            }
            data.projectileStartEffectVariants = prefabs;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/start-effects.txt", "PASS: Start/Start1/Start2를 투사체 이미지 1/2/3과 연결, 크기 0.8배, 18fps 단회 재생 종료 Animation Event로 발사");
        }

        [MenuItem("Tools/Nytherion/Homing Magic/Apply Three Animation Variants")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(HomingMagicSetup.DataPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomingMagicSetup.ProjectilePath);
            RuntimeAnimatorController original = prefab.GetComponent<Animator>().runtimeAnimatorController;
            AnimationClip originalClip = original.animationClips.First();
            var variants = new RuntimeAnimatorController[3];
            Sprite firstFrame = null;
            for (int i = 0; i < variants.Length; i++)
            {
                string suffix = i == 0 ? "" : " " + i;
                Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/Nytherion/Art/Combat/VFX/Sprites/Spirits'GuidanceProj" + suffix + ".png")
                    .OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
                if (frames.Length != 4) throw new InvalidOperationException("투사체 시트의 4프레임을 확인해 주세요: " + suffix);
                if (i == 0) firstFrame = frames[0];
                string clipPath = Folder + "/SpiritsGuidanceLoop" + (i + 1) + ".anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = UnityEngine.Object.Instantiate(originalClip);
                    clip.name = "SpiritsGuidanceLoop" + (i + 1);
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                var keys = new ObjectReferenceKeyframe[frames.Length];
                for (int frame = 0; frame < frames.Length; frame++)
                    keys[frame] = new ObjectReferenceKeyframe { time = frame / clip.frameRate, value = frames[frame] };
                AnimationUtility.SetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                string controllerPath = Folder + "/SpiritsGuidanceVariant" + (i + 1) + ".overrideController";
                AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
                if (controller == null)
                {
                    controller = new AnimatorOverrideController(original);
                    AssetDatabase.CreateAsset(controller, controllerPath);
                }
                controller[originalClip.name] = clip;
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);
                variants[i] = controller;
            }
            data.projectileAnimationVariants = variants;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            GameObject root = PrefabUtility.LoadPrefabContents(HomingMagicSetup.ProjectilePath);
            try
            {
                root.GetComponent<SpriteRenderer>().sprite = firstFrame;
                PrefabUtility.SaveAsPrefabAsset(root, HomingMagicSetup.ProjectilePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/variants.txt", "PASS: 3종 4프레임 애니메이션을 발사 순서대로 연결");
        }
    }
}
