using System;
using System.IO;
using System.Linq;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class EvilEyeHitEffectSetup
    {
        private const string Output = "output/evil-eye-hit";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/EvilEyeHit.png";
        private const string ClipPath = "Assets/Nytherion/Art/Combat/VFX/Animations/EvilEyeHit.anim";
        private const string ControllerPath = "Assets/Nytherion/Art/Combat/VFX/Animations/EvilEyeHit.controller";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/EvilEyeHitVFX.prefab";
        private const string CommonPrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/ExplosionVFX.prefab";
        private const string PoolPath = "Assets/Prefabs/Infrastructure/Managers/ObjectPoolManager.prefab";
        private const string ProjectilePath = "Assets/Prefabs/Gameplay/Combat/Proj/EvilEyeProj.prefab";
        private const string PoolTag = "Player_EvilEyeHitVFX";
        private const float Fps = 12f;

        static EvilEyeHitEffectSetup() => EditorApplication.update += ProcessRequest;

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

        [MenuItem("Tools/Nytherion/EvilEye/타격 애니메이션 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            Require(sheet != null && sheet.width == 256 && sheet.height == 64, "원본 시트 크기");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 32f);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            Require(frames.Length == 4, "64×64 4프레임");

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "EvilEyeHit" };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            clip.frameRate = Fps;
            clip.wrapMode = WrapMode.Once;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("Visual", typeof(SpriteRenderer), "m_Sprite"),
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
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            // 기존 설정을 복제하되 독립 GUID를 발급하고 공용 프리팹 참조는 유지합니다.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                Require(AssetDatabase.CopyAsset(CommonPrefabPath, PrefabPath), "이블아이 전용 프리팹 생성");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.name = "EvilEyeHitVFX";
                float radius = root.GetComponent<CircleCollider2D>().radius;
                Transform visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    visual = new GameObject("Visual").transform;
                    visual.SetParent(root.transform, false);
                }
                var renderer = visual.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
                var previous = root.GetComponent<SpriteRenderer>();
                if (previous != null)
                {
                    EditorUtility.CopySerialized(previous, renderer);
                    UnityEngine.Object.DestroyImmediate(previous);
                }
                renderer.sprite = frames[0];
                renderer.sortingOrder = WeaponVFXHelper.HitEffectSortingOrder;
                Animator animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var oldReturn = root.GetComponent<PooledExplosionVFX>();
                if (oldReturn != null) UnityEngine.Object.DestroyImmediate(oldReturn);
                var pooled = root.GetComponent<PooledVFXAnimationEvent>();
                if (pooled == null) pooled = root.AddComponent<PooledVFXAnimationEvent>();
                var serialized = new SerializedObject(pooled);
                serialized.FindProperty("poolTag").stringValue = PoolTag;
                serialized.FindProperty("animationStates").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var drift = root.GetComponent<EvilEyeHitDrift>();
                if (drift == null) drift = root.AddComponent<EvilEyeHitDrift>();
                serialized = new SerializedObject(drift);
                serialized.FindProperty("visual").objectReferenceValue = visual;
                serialized.FindProperty("hitEffectOffsetRadius").floatValue = 0.18f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Require(root.GetComponent<ExplosionDamage>() != null && root.GetComponent<CircleCollider2D>().radius == radius,
                    "기존 폭발 피해 판정 유지");
                for (int i = 0; i < frames.Length; i++)
                {
                    clip.SampleAnimation(root, (i + 0.5f) / Fps);
                    Require(renderer.sprite == frames[i] && frames[i].pivot == new Vector2(32f, 32f), "프레임 순서·중앙 피벗 " + i);
                }
                renderer.sprite = frames[0];
                for (int i = 0; i < 16; i++)
                {
                    drift.SendMessage("OnEnable");
                    Require(visual.localPosition.magnitude <= 0.1801f && visual.localPosition.z == 0f,
                        "생성 위치 무작위 오프셋 범위");
                    drift.SendMessage("OnDisable");
                    Require(visual.localPosition == Vector3.zero, "재사용 시 오프셋 초기화");
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            RestoreCommonPrefab();
            ConfigureLinks();
            Require(Mathf.Abs(clip.length - 1f / 3f) < 0.001f, "재생 길이");
            File.WriteAllText(Output + "/verification.txt",
                "PASS 64×64 4프레임 / 중앙 피벗 / Point / 12fps / 0.333초 단발\n" +
                "PASS 프레임 샘플링 / 기존 폭발 피해 유지 / Player_EvilEyeHitVFX 전용 풀·투사체 연결 / 풀 반환 이벤트\n" +
                "PASS 공용 ExplosionVFX 기존 이미지·애니메이터·풀 반환 복원 / 전용 프리팹 독립 GUID\n" +
                "PASS 시각 생성 위치만 반경 0.18유닛 내 무작위 선택 / 재생 중 고정 / 16회 오프셋·초기화 검증\n");
            Debug.Log("[EvilEyeHitEffectSetup] 이블아이 타격 애니메이션 연결 및 검증 완료");
        }

        private static void RestoreCommonPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CommonPrefabPath);
            try
            {
                // 최초 분리 시에만 이블아이 전용 설정을 제거합니다.
                var drift = root.GetComponent<EvilEyeHitDrift>();
                if (drift == null) return;
                UnityEngine.Object.DestroyImmediate(drift);
                var pooled = root.GetComponent<PooledVFXAnimationEvent>();
                if (pooled != null) UnityEngine.Object.DestroyImmediate(pooled);
                Transform visual = root.transform.Find("Visual");
                var renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                if (visual != null)
                {
                    EditorUtility.CopySerialized(visual.GetComponent<SpriteRenderer>(), renderer);
                    UnityEngine.Object.DestroyImmediate(visual.gameObject);
                }
                renderer.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Nytherion/Art/Combat/VFX/Sprites/Explosion.png")
                    .OfType<Sprite>().Single(sprite =>
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id) && id == 67403793);
                renderer.sortingOrder = 0;
                root.GetComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Nytherion/Art/Combat/VFX/Animations/ExplosionVFX.controller");
                root.AddComponent<PooledExplosionVFX>().poolTag = "Player_ExplosionVFX";
                PrefabUtility.SaveAsPrefabAsset(root, CommonPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureLinks()
        {
            GameObject dedicated = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject common = AssetDatabase.LoadAssetAtPath<GameObject>(CommonPrefabPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PoolPath);
            try
            {
                var manager = root.GetComponent<ObjectPoolManager>();
                Require(manager.pools.Single(pool => pool.tag == "Player_ExplosionVFX").prefab == common,
                    "공용 풀 기존 참조 유지");
                var pool = manager.pools.SingleOrDefault(entry => entry.tag == PoolTag);
                if (pool == null)
                {
                    pool = new ObjectPoolManager.Pool { tag = PoolTag, size = 30 };
                    manager.pools.Add(pool);
                }
                pool.prefab = dedicated;
                PrefabUtility.SaveAsPrefabAsset(root, PoolPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            root = PrefabUtility.LoadPrefabContents(ProjectilePath);
            try
            {
                root.GetComponent<ExplosionModifier>().explosionVisualPoolTag = PoolTag;
                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Require(dedicated != common && common.GetComponent<SpriteRenderer>() != null &&
                common.GetComponent<PooledExplosionVFX>() != null && common.GetComponent<EvilEyeHitDrift>() == null,
                "공용·전용 프리팹 분리");
            Require(!AssetDatabase.GetDependencies(CommonPrefabPath).Contains(ClipPath) &&
                AssetDatabase.GetDependencies(PrefabPath).Contains(ClipPath), "이블아이 애니메이션 참조 격리");
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("이블아이 타격 검증 실패: " + detail);
        }
    }
}
