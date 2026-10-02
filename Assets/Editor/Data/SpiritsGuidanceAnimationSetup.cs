using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
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
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/variants.request")) return;
            File.Delete(Output + "/variants.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/variants.txt", error.ToString());
                Debug.LogException(error);
            }
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
