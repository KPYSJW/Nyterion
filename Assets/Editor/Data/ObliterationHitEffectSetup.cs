using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ObliterationHitEffectSetup
    {
        private const string Output = "output/obliteration-hit";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/ObliterationHit.png";
        private const string ClipPath = "Assets/Nytherion/Art/Combat/VFX/Animations/ObliterationHit.anim";
        private const string ControllerPath = "Assets/Nytherion/Art/Combat/VFX/Animations/ObliterationHit.controller";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/ObliterationHitVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/Obliteration.asset";
        private const float FramesPerSecond = 20f;

        static ObliterationHitEffectSetup() => EditorApplication.update += ProcessRequest;

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

        [MenuItem("Tools/Nytherion/Obliteration/타격 애니메이션 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            Require(sheet != null && sheet.width == 160 && sheet.height == 32, "160×32 원본 시트");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(32, 32), 32f);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            Require(frames.Length == 5, "32×32 5프레임");

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "ObliterationHit" };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            clip.frameRate = FramesPerSecond;
            clip.wrapMode = WrapMode.Once;
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding,
                frames.Select((frame, index) => new ObjectReferenceKeyframe
                { time = index / FramesPerSecond, value = frame }).ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.startTime = 0f;
            settings.stopTime = frames.Length / FramesPerSecond;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            // 마지막 프레임까지 표시한 뒤 기존 타격 이펙트 수명 주기로 풀에 반환합니다.
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent
            {
                time = frames.Length / FramesPerSecond,
                functionName = nameof(PooledVFXAnimationEvent.AnimationFinished)
            } });
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Select(entry => entry.state).FirstOrDefault(entry => entry.name == "Hit");
            if (state == null) state = machine.AddState("Hit");
            state.motion = clip;
            state.speed = 1f;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("ObliterationHitVFX");
            if (existing == null) SceneManager.MoveGameObjectToScene(root, preview);
            GameObject prefab;
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.color = Color.white;
                renderer.sortingOrder = WeaponVFXHelper.HitEffectSortingOrder;
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                Animator animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                var effect = root.GetComponent<PooledVFXAnimationEvent>();
                if (effect == null) effect = root.AddComponent<PooledVFXAnimationEvent>();
                var serialized = new SerializedObject(effect);
                serialized.FindProperty("poolTag").stringValue = "ObliterationHitVFX";
                serialized.FindProperty("animationStates").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            ObliterationWeaponData data = AssetDatabase.LoadAssetAtPath<ObliterationWeaponData>(DataPath);
            Require(data != null && prefab != null, "무기·타격 프리팹");
            data.hitEffectPrefab = prefab;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            Require(data.hitEffectPrefab.GetComponent<Animator>().runtimeAnimatorController == controller &&
                prefab.GetComponent<PooledVFXAnimationEvent>() != null, "애니메이터·풀 반환 연결");
            Require(!settings.loopTime && Mathf.Abs(clip.length - 0.25f) < 0.001f, "단발 재생 길이");
            Require(AnimationUtility.GetAnimationEvents(clip).Single().time == clip.length, "마지막 프레임 뒤 반환");
            preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject sample = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(sample, preview);
                for (int i = 0; i < frames.Length; i++)
                {
                    Require(frames[i].pivot == new Vector2(16f, 16f), "프레임 중앙 피벗");
                    clip.SampleAnimation(sample, (i + 0.5f) / FramesPerSecond);
                    Require(sample.GetComponent<SpriteRenderer>().sprite == frames[i], "프레임 재생 순서 " + i);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            File.WriteAllText(Output + "/verification.txt",
                "PASS 32×32 5프레임·중앙 피벗·20fps·0.25초 단발 재생\n" +
                "PASS 프레임 샘플링·Animator/무기 참조·종료 후 풀 반환 이벤트\n");
            Debug.Log("[ObliterationHitEffectSetup] 타격 애니메이션 연결 및 에셋 검증 완료");
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Obliteration 타격 검증 실패: " + detail);
        }
    }
}
