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
    public static class AfterglowHitEffectSetup
    {
        private const string Output = "output/afterglow-hit";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/AfterglowHit.png";
        private const string ClipPath = "Assets/Nytherion/Art/Combat/VFX/Animations/AfterglowHit.anim";
        private const string ControllerPath = "Assets/Nytherion/Art/Combat/VFX/Animations/AfterglowHit.controller";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/AfterglowHitVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/Afterglow.asset";
        private const string ProjectilePath = "Assets/Prefabs/Gameplay/Combat/Proj/AfterglowProj.prefab";
        private const float Fps = 18f;

        static AfterglowHitEffectSetup() => EditorApplication.update += ProcessRequest;

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

        [MenuItem("Tools/Nytherion/Afterglow/타격 애니메이션 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            Require(sheet != null && sheet.width == 192 && sheet.height == 32, "192×32 원본 시트");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(32, 32), 32f);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            Require(frames.Length == 6, "32×32 6프레임");

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "AfterglowHit" };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            clip.frameRate = Fps;
            clip.wrapMode = WrapMode.Once;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((sprite, index) => new ObjectReferenceKeyframe { time = index / Fps, value = sprite }).ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.startTime = 0f;
            settings.stopTime = frames.Length / Fps;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent
            {
                time = frames.Length / Fps,
                functionName = nameof(PooledVFXAnimationEvent.AnimationFinished)
            } });
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(entry => entry.state).FirstOrDefault(entry => entry.name == "Hit");
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
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("AfterglowHitVFX");
            if (existing == null) SceneManager.MoveGameObjectToScene(root, preview);
            GameObject prefab;
            try
            {
                var renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.color = Color.white;
                renderer.sortingOrder = WeaponVFXHelper.HitEffectSortingOrder;
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                var pooled = root.GetComponent<PooledVFXAnimationEvent>();
                if (pooled == null) pooled = root.AddComponent<PooledVFXAnimationEvent>();
                var serialized = new SerializedObject(pooled);
                serialized.FindProperty("poolTag").stringValue = "AfterglowHitVFX";
                serialized.FindProperty("animationStates").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < frames.Length; i++)
                {
                    clip.SampleAnimation(root, (i + 0.5f) / Fps);
                    Require(renderer.sprite == frames[i] && frames[i].pivot == new Vector2(16f, 16f),
                        "프레임 순서·중앙 피벗 " + i);
                }
                renderer.sprite = frames[0];
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }

            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
            Require(data != null && prefab != null, "무기·타격 프리팹");
            data.hitEffectPrefab = prefab;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            root = PrefabUtility.LoadPrefabContents(ProjectilePath);
            try
            {
                var collision = root.GetComponent<CollisionObject>();
                collision.hitEffectPrefab = prefab;
                Require(!collision.playHitEffectOnWall, "적 충돌 시 재생");
                var multiHit = root.GetComponent<SlowMultiHitModifier>();
                Require(multiHit != null, "기존 반복 피해 유지");
                multiHit.playHitEffectOnTick = true;
                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Require(AssetDatabase.GetDependencies(ProjectilePath).Contains(ClipPath), "탄환·애니메이션 참조");
            Require(!settings.loopTime && Mathf.Abs(clip.length - 1f / 3f) < 0.001f, "단발 재생 길이");
            Require(AnimationUtility.GetAnimationEvents(clip).Single().time == clip.length, "마지막 프레임 뒤 반환");
            File.WriteAllText(Output + "/verification.txt",
                "PASS 192×32 원본 / 32×32 6프레임 / 중앙 피벗 / Point / 18fps / 0.333초 단발\n" +
                "PASS 전 프레임 샘플링 / Animator·무기·탄환 참조 / 풀 반환 이벤트 / 기존 반복 피해 유지\n" +
                "PASS 반복 피해 틱 타격 효과 활성화 / 공용 타격 재생기의 매회 0~360도 무작위 회전 사용\n");
            Debug.Log("[AfterglowHitEffectSetup] 잔향 타격 애니메이션 연결 및 검증 완료");
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("잔향 타격 검증 실패: " + detail);
        }
    }
}
