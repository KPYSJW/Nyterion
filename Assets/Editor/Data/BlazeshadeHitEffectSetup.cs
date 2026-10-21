using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class BlazeshadeHitEffectSetup
    {
        private const string Output = "output/blazeshade-hit";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/BlazeshadeHit.png";
        private const string ClipPath = "Assets/Nytherion/Art/Combat/VFX/Animations/BlazeshadeHit.anim";
        private const string ControllerPath = "Assets/Nytherion/Art/Combat/VFX/Animations/BlazeshadeHit.controller";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/BlazeshadeHitVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/Blazeshade.asset";
        private const float FramesPerSecond = 18f;

        static BlazeshadeHitEffectSetup() => EditorApplication.update += ProcessRequest;

        private static void ProcessRequest()
        {
            string request = File.Exists(Output + "/range-verification.request")
                ? Output + "/range-verification.request" : Output + "/setup.request";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            File.Delete(request);
            try
            {
                if (request.EndsWith("range-verification.request")) VerifyDamageRange();
                else Apply();
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + (request.EndsWith("range-verification.request")
                    ? "/range-verification.txt" : "/verification.txt"), "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Blazeshade/타격 애니메이션 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            Require(sheet != null && sheet.width == 576 && sheet.height == 64, "576×64 원본 시트");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 64f);
            // 모든 프레임의 바닥 기준을 유지해 뒤쪽 불꽃이 자연스럽게 위로 사라집니다.
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] rects = provider.GetSpriteRects();
            foreach (SpriteRect rect in rects)
            {
                rect.alignment = SpriteAlignment.Custom;
                rect.pivot = new Vector2(0.5f, 0f);
            }
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            Require(frames.Length == 9, "64×64 9프레임");

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "BlazeshadeHit" };
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
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("BlazeshadeHitVFX");
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
                serialized.FindProperty("poolTag").stringValue = "BlazeshadeHitVFX";
                serialized.FindProperty("animationStates").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
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

            Require(data.hitEffectPrefab.GetComponent<Animator>().runtimeAnimatorController == controller &&
                prefab.GetComponent<PooledVFXAnimationEvent>() != null, "애니메이터·풀 반환 연결");
            Require(!settings.loopTime && Mathf.Abs(clip.length - 0.5f) < 0.001f, "단발 재생 길이");
            Require(AnimationUtility.GetAnimationEvents(clip).Single().time == clip.length, "마지막 프레임 뒤 반환");
            preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject sample = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(sample, preview);
                GameObject monster = new GameObject("발 위치 검증");
                SceneManager.MoveGameObjectToScene(monster, preview);
                monster.transform.position = new Vector3(3f, 5f, 0f);
                monster.transform.localScale = new Vector3(2f, 3f, 1f);
                SpriteRenderer body = monster.AddComponent<SpriteRenderer>();
                body.sprite = frames[0];
                EnemyBase enemy = monster.AddComponent<EnemyBase>();
                var enemySettings = new SerializedObject(enemy);
                enemySettings.FindProperty("spriteRenderer").objectReferenceValue = body;
                enemySettings.ApplyModifiedPropertiesWithoutUndo();
                sample.transform.position = enemy.GetFootPosition();
                sample.transform.localScale = Vector3.one * 1.7f;
                Require(Mathf.Abs(sample.transform.position.y - body.bounds.min.y) < 0.001f,
                    "몬스터 본체 하단에서 생성");
                for (int i = 0; i < frames.Length; i++)
                {
                    Require(frames[i].pivot == new Vector2(32f, 0f), "프레임 하단 중앙 피벗");
                    clip.SampleAnimation(sample, (i + 0.5f) / FramesPerSecond);
                    Require(sample.GetComponent<SpriteRenderer>().sprite == frames[i], "프레임 재생 순서 " + i);
                    Require(Mathf.Abs(sample.GetComponent<SpriteRenderer>().bounds.min.y - body.bounds.min.y) < 0.001f,
                        "크기 변경 후 이펙트 하단과 몬스터 발 정렬 " + i);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            File.WriteAllText(Output + "/verification.txt",
                "PASS 64×64 9프레임·하단 중앙 피벗·18fps·0.5초 단발 재생\n" +
                "PASS 프레임 샘플링·Animator/무기 참조·종료 후 풀 반환 이벤트\n" +
                "PASS 몬스터 크기·이펙트 크기 변경 후 전 프레임 발 위치 정렬\n");
            Debug.Log("[BlazeshadeHitEffectSetup] 타격 애니메이션 연결 및 에셋 검증 완료");
        }

        [MenuItem("Tools/Nytherion/Blazeshade/타격 범위 검증")]
        public static void VerifyDamageRange()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            float ratio = (float)typeof(BlazeshadeWeapon).GetField("DamageRadiusRatio", flags).GetRawConstantValue();
            MethodInfo overlaps = typeof(BlazeshadeWeapon).GetMethod("IsWithinDamageRange", flags);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject monster = new GameObject("불그늘 범위 검증");
                SceneManager.MoveGameObjectToScene(monster, preview);
                BoxCollider2D body = monster.AddComponent<BoxCollider2D>();
                body.size = Vector2.one * 0.5f;
                CircleCollider2D sensor = monster.AddComponent<CircleCollider2D>();
                sensor.radius = 5f;
                sensor.isTrigger = true;
                EnemyBase enemy = monster.AddComponent<EnemyBase>();
                var settings = new SerializedObject(enemy);
                settings.FindProperty("groundBodyCollider").objectReferenceValue = body;
                settings.ApplyModifiedPropertiesWithoutUndo();
                foreach (float size in new[] { 0.5f, 1f, 2f })
                {
                    float radius = 2.75f * size * ratio;
                    monster.transform.position = new Vector3(radius + 0.3f, 0f, 0f);
                    Physics2D.SyncTransforms();
                    Require(!(bool)overlaps.Invoke(null, new object[] { enemy, sensor, Vector2.zero, radius }),
                        "큰 감지 콜라이더만 닿은 적 제외 " + size);
                    monster.transform.position = new Vector3(radius + 0.2f, 0f, 0f);
                    Physics2D.SyncTransforms();
                    Require((bool)overlaps.Invoke(null, new object[] { enemy, sensor, Vector2.zero, radius }),
                        "본체가 불꽃 경계에 닿은 적 타격 " + size);
                }
                Require(Mathf.Abs(2.75f * ratio - 1.93359375f) < 0.0001f, "이미지 기준 반경");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/range-verification.txt",
                "PASS 불꽃 45/64 비율 적용: 기본 반경 2.75 → 1.93359375\n" +
                "PASS 범위 배율 0.5·1·2에서 감지 콜라이더만 닿은 적 제외·본체 경계 접촉 타격\n");
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Blazeshade 타격 검증 실패: " + detail);
        }
    }
}
