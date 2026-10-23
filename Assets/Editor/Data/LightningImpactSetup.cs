using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class LightningImpactSetup
    {
        private const string Output = "output/lightning-impact";
        private const string Sheet = "Assets/Nytherion/Art/Combat/VFX/Sprites/LightningImpactSheet.png";
        private const string Clip = "Assets/Nytherion/Art/Combat/VFX/Animations/LightningImpact.anim";
        private const string Controller = "Assets/Nytherion/Art/Combat/VFX/Animations/LightningImpact.controller";
        private const string Prefab = "Assets/Prefabs/Gameplay/Combat/VFX/LightningImpactVFX.prefab";
        private static readonly string[] Beams =
        {
            "Assets/Prefabs/Gameplay/Combat/VFX/ChainLightningVFX.prefab",
            "Assets/Prefabs/Gameplay/Combat/VFX/ChainLightningSpriteVFX.prefab"
        };

        static LightningImpactSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { CreateAndAssign(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/번개 이펙트/대상 중심 피격 번개 설정")]
        public static void CreateAndAssign()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 설정해 주세요.");
            Directory.CreateDirectory(Output);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
            Require(sheet != null && sheet.width == 128 && sheet.height == 32, "128×32 원본 시트");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(32, 32), 32f);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            Require(frames.Length == 4 && frames.All(frame => frame.rect.size == new Vector2(32f, 32f)), "32×32 4프레임");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clip);
            if (clip == null) { clip = new AnimationClip { name = "LightningImpact" }; AssetDatabase.CreateAsset(clip, Clip); }
            clip.frameRate = 20f;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 20f, value = frame }).ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            clipSettings.startTime = 0f;
            clipSettings.stopTime = frames.Length / 20f;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(Controller);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == "Impact");
            if (state == null) state = machine.AddState("Impact");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(Prefab) : new GameObject("LightningImpactVFX");
            GameObject saved;
            try
            {
                LightningImpactVFX effect = root.GetComponent<LightningImpactVFX>();
                if (effect == null) effect = root.AddComponent<LightningImpactVFX>();
                SerializedObject serialized = new SerializedObject(effect);
                serialized.FindProperty("animationClip").objectReferenceValue = clip;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                renderer.sortingOrder = WeaponVFXHelper.HitEffectSortingOrder;
                Animator animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = false;
                saved = PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            foreach (string beam in Beams)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(beam);
                try
                {
                    SerializedObject serialized = new SerializedObject(contents.GetComponent<ChainLightningVFX>());
                    serialized.FindProperty("impactEffectPrefab").objectReferenceValue = saved.GetComponent<LightningImpactVFX>();
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents, beam);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(beam).GetComponent<ChainLightningVFX>().HasImpactEffect, "번개 프리팹 피격 애니메이션 연결");
            }
            Verify(saved, clip, frames);
            Debug.Log("[LightningImpactSetup] 적 몸체 중심 피격 번개 설정 및 검증 완료");
        }

        private static void Verify(GameObject prefab, AnimationClip clip, Sprite[] frames)
        {
            Require(Mathf.Abs(clip.length - 0.2f) < 0.001f && !AnimationUtility.GetAnimationClipSettings(clip).loopTime,
                "20fps·0.2초 단발 애니메이션");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject poolObject = new GameObject("검증 풀");
                SceneManager.MoveGameObjectToScene(poolObject, preview);
                ObjectPoolManager pool = poolObject.AddComponent<ObjectPoolManager>();
                Call(pool, "OnInitializeInternal");
                GameObject target = new GameObject("검증 적");
                SceneManager.MoveGameObjectToScene(target, preview);
                EnemyBase enemy = target.AddComponent<EnemyBase>();
                GameObject body = new GameObject("몸체");
                body.transform.SetParent(target.transform, false);
                body.transform.localPosition = Vector3.up * 0.6f;
                SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
                bodyRenderer.sprite = frames[0];
                bodyRenderer.sortingOrder = 123;
                GameObject instance = pool.SpawnFromPool(prefab, Vector3.zero, Quaternion.identity, 1);
                LightningImpactVFX effect = instance.GetComponent<LightningImpactVFX>();
                effect.Play(pool, enemy);
                Require(Vector3.Distance(instance.transform.position, bodyRenderer.bounds.center) < 0.001f &&
                    instance.GetComponent<SpriteRenderer>().sortingOrder == 124, "몸체 중심 위치·적보다 앞에 표시");
                for (int i = 0; i < frames.Length; i++)
                {
                    clip.SampleAnimation(instance, (i + 0.1f) / 20f);
                    Require(instance.GetComponent<SpriteRenderer>().sprite == frames[i], "피격 번개 프레임 " + i);
                }
                target.transform.position = new Vector3(3f, 2f);
                Call(effect, "FollowTarget");
                Require(Vector3.Distance(instance.transform.position, bodyRenderer.bounds.center) < 0.001f, "움직이는 적 중심 추적");
                typeof(LightningImpactVFX).GetField("spawnFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(effect, Time.frameCount - 1);
                typeof(LightningImpactVFX).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(effect, clip.length + 1f);
                Call(effect, "LateUpdate");
                Require(!instance.activeSelf, "애니메이션 종료 후 풀 반환");
                GameObject reused = pool.SpawnFromPool(prefab, Vector3.zero, Quaternion.identity, 1);
                reused.GetComponent<LightningImpactVFX>().Play(pool, enemy);
                Require(reused == instance && reused.GetComponent<SpriteRenderer>().sprite == frames[0], "풀 재사용 첫 프레임 복원");
                target.SetActive(false);
                Call(effect, "LateUpdate");
                Require(!instance.activeSelf, "적 비활성화 후 피격 이펙트 정리");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            File.WriteAllText(Output + "/verification.txt", "PASS 32×32 4프레임·20fps·0.2초 단발 애니메이션\nPASS 적 몸체 중심·대상 이동 추적·몸체보다 앞에 표시\nPASS 애니메이션 종료/적 비활성화 시 풀 반환·재사용 첫 프레임 복원\nPASS 기존/새 번개 프리팹에 피격 효과 연결\n");
        }

        private static void Call(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void Require(bool success, string label)
        {
            if (!success) throw new InvalidOperationException("피격 번개 검증 실패: " + label);
        }
    }
}
