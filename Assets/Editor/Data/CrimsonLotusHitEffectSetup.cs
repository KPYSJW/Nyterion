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
    public static class CrimsonLotusHitEffectSetup
    {
        private const string Output = "output/crimson-lotus-hit";
        private const string AnimationRoot = "Assets/Nytherion/Art/Combat/VFX/Animations";
        private const string ControllerPath = AnimationRoot + "/CrimsonLotusHit.controller";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/CrimsonLotusHitVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/CrimsonLotus.asset";
        private static readonly string[] States = { "Hit1", "Hit3" };
        private static string SheetPath(int index) => $"Assets/Nytherion/Art/Combat/VFX/Sprites/CrimsonLotus{States[index]}.png";

        static CrimsonLotusHitEffectSetup() => EditorApplication.update += ProcessRequest;

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

        [MenuItem("Tools/Nytherion/CrimsonLotus/타격 애니메이션 2종 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState entry in machine.states)
            {
                if (entry.state.name == "Hit2") machine.RemoveState(entry.state);
            }
            Sprite firstSprite = null;
            for (int i = 0; i < States.Length; i++)
            {
                string path = SheetPath(i);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int count = 6;
                Require(sheet != null && sheet.width == count * 64 && sheet.height == 64, "64×64 가로 시트");
                WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 64f);
                Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
                Require(frames.Length == count, "시트 프레임 수");
                if (i == 0) firstSprite = frames[0];
                string clipPath = AnimationRoot + $"/CrimsonLotus{States[i]}.anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip { name = "CrimsonLotus" + States[i] };
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                clip.frameRate = 20f;
                clip.wrapMode = WrapMode.Once;
                AnimationUtility.SetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                    frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 20f, value = frame }).ToArray());
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                settings.startTime = 0f;
                settings.stopTime = count / 20f;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                // 마지막 프레임도 한 프레임 동안 표시한 뒤 풀로 반환합니다.
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent
                {
                    time = count / 20f, functionName = nameof(PooledVFXAnimationEvent.AnimationFinished)
                } });
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                AnimatorState state = machine.states.Select(entry => entry.state).FirstOrDefault(entry => entry.name == States[i]);
                if (state == null) state = machine.AddState(States[i]);
                state.motion = clip;
                state.speed = 1f;
                if (i == 0) machine.defaultState = state;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("CrimsonLotusHitVFX");
            if (existing == null) SceneManager.MoveGameObjectToScene(root, preview);
            GameObject prefab;
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = firstSprite;
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
                serialized.FindProperty("poolTag").stringValue = "CrimsonLotusHitVFX";
                SerializedProperty states = serialized.FindProperty("animationStates");
                states.arraySize = States.Length;
                for (int i = 0; i < States.Length; i++) states.GetArrayElementAtIndex(i).stringValue = States[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            CrimsonLotusWeaponData data = AssetDatabase.LoadAssetAtPath<CrimsonLotusWeaponData>(DataPath);
            Require(data != null && prefab != null, "무기·타격 프리팹");
            data.hitEffectPrefab = prefab;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Verify();
        }

        public static void Verify()
        {
            var data = AssetDatabase.LoadAssetAtPath<CrimsonLotusWeaponData>(DataPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(data != null && data.hitEffectPrefab == prefab && prefab != null, "무기 타격 참조");
            var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
            Require(controller != null && controller.layers[0].stateMachine.states.Length == States.Length, "Animator 2개 상태");
            var serialized = new SerializedObject(prefab.GetComponent<PooledVFXAnimationEvent>());
            var states = serialized.FindProperty("animationStates");
            Require(states.arraySize == States.Length, "무작위 선택 2종");
            for (int i = 0; i < States.Length; i++)
            {
                Require(states.GetArrayElementAtIndex(i).stringValue == States[i], "무작위 상태 이름");
                var state = controller.layers[0].stateMachine.states.Single(entry => entry.state.name == States[i]).state;
                var clip = state.motion as AnimationClip;
                int count = 6;
                var frames = WeaponFireEffectAssets.LoadFrames(AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath(i)));
                var keys = AnimationUtility.GetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
                Require(keys.Length == count && keys.Select((key, index) => key.value == frames[index]).All(value => value), "프레임 순서");
                Require(!AnimationUtility.GetAnimationClipSettings(clip).loopTime && Mathf.Abs(clip.length - count / 20f) < 0.001f, "재생 길이·반복 없음");
                Require(AnimationUtility.GetAnimationEvents(clip).Single().functionName == nameof(PooledVFXAnimationEvent.AnimationFinished), "풀 반환 이벤트");
            }
            File.WriteAllText(Output + "/verification.txt",
                "PASS 64×64 원본 시트 2종(Hit1·Hit3): 각각 6프레임, 20fps, 0.30초\n" +
                "PASS 프레임 순서·반복 없음·종료 이벤트·Animator 상태·무작위 2종·CrimsonLotus 타격 참조\n");
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("CrimsonLotus 타격 검증 실패: " + detail);
        }
    }
}
