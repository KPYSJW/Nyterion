using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ArcaneDartHitEffectSetup
    {
        private const string Output = "output/arcane-dart-hit";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/ArcaneDartHit.png";
        private const string ClipPath = "Assets/Nytherion/Art/Combat/VFX/Animations/ArcaneDartHit.anim";
        private const string ControllerPath = "Assets/Nytherion/Art/Combat/VFX/Animations/ArcaneDartHit.controller";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/ArcaneDartHitVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/ArcaneDart.asset";
        private const string ProjectilePath = "Assets/Prefabs/Gameplay/Combat/Proj/ArcaneDartProj.prefab";
        private const float Fps = 12f;

        static ArcaneDartHitEffectSetup() => EditorApplication.update += ProcessRequest;

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

        [MenuItem("Tools/Nytherion/ArcaneDart/타격 애니메이션 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            Require(sheet != null && sheet.width == 192 && sheet.height == 64, "192×64 원본 시트");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 32f);
            // 시트의 불꽃이 시작되는 지점을 실제 탄환 접촉점에 맞춥니다.
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] rects = provider.GetSpriteRects();
            foreach (SpriteRect rect in rects)
            {
                rect.alignment = SpriteAlignment.Custom;
                rect.pivot = new Vector2(0.375f, 0.5f);
            }
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            Require(frames.Length == 3, "64×64 3프레임");

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "ArcaneDartHit" };
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
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("ArcaneDartHitVFX");
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
                serialized.FindProperty("poolTag").stringValue = "ArcaneDartHitVFX";
                serialized.FindProperty("animationStates").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < frames.Length; i++)
                {
                    clip.SampleAnimation(root, (i + 0.5f) / Fps);
                    Require(renderer.sprite == frames[i] && frames[i].pivot == new Vector2(24f, 32f),
                        "프레임 순서·타격점 피벗 " + i);
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
                collision.alignHitEffectToIncomingDirection = true;
                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Require(AssetDatabase.GetDependencies(ProjectilePath).Contains(ClipPath), "탄환·애니메이션 참조");
            Require(!settings.loopTime && Mathf.Abs(clip.length - 0.25f) < 0.001f, "단발 재생 길이");
            Require(AnimationUtility.GetAnimationEvents(clip).Single().time == clip.length, "마지막 프레임 뒤 반환");
            File.WriteAllText(Output + "/verification.txt",
                "PASS 192×64 원본 / 64×64 3프레임 / 타격점 피벗 / Point / 12fps / 0.25초 단발\n" +
                "PASS 전 프레임 샘플링 / Animator·무기·탄환 참조 / 풀 반환 이벤트\n" +
                "PASS 탄환의 역방향으로 타격 효과 오른쪽 축 정렬 활성화\n");
            Debug.Log("[ArcaneDartHitEffectSetup] 비전 화살 타격 애니메이션 연결 및 검증 완료");
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("비전 화살 타격 검증 실패: " + detail);
        }
    }
}

