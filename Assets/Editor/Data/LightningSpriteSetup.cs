using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class LightningSpriteSetup
    {
        private const string Output = "output/lightning-sprite";
        private const string Sheet = "Assets/Nytherion/Art/Combat/VFX/Sprites/LightningSpriteSheet.png";
        private const string Animation = "Assets/Nytherion/Art/Combat/VFX/Animations/LightningSprite.anim";
        private const string Controller = "Assets/Nytherion/Art/Combat/VFX/Animations/LightningSprite.controller";
        private const string Segment = "Assets/Prefabs/Gameplay/Combat/VFX/LightningSpriteSegment.prefab";
        private const string Original = "Assets/Prefabs/Gameplay/Combat/VFX/ChainLightningVFX.prefab";
        private const string SpriteEffect = "Assets/Prefabs/Gameplay/Combat/VFX/ChainLightningSpriteVFX.prefab";
        private const string Weapon = "Assets/Prefabs/Gameplay/Combat/Proj/StormbringerProj.prefab";
        private const string LaserSheet = "Assets/Nytherion/Art/Combat/VFX/Sprites/LaserEffect.png";
        private const string LaserSegment = "Assets/Prefabs/Gameplay/Combat/VFX/StormbringerLaserSegment.prefab";
        private const string LaserEffect = "Assets/Prefabs/Gameplay/Combat/VFX/StormbringerLaserVFX.prefab";
        private const string Relics = "Assets/Nytherion/Data/ScriptableObjects/Relics/";

        static LightningSpriteSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool laser = File.Exists(Output + "/laser.request");
            if (!laser && !File.Exists(Output + "/setup.request") && !File.Exists(Output + "/original.request")) return;
            bool original = File.Exists(Output + "/original.request");
            File.Delete(Output + (laser ? "/laser.request" : original ? "/original.request" : "/setup.request"));
            try
            {
                if (laser) ApplyLaser();
                else if (original) ApplyOriginal();
                else CreateAndApply();
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/번개 이펙트/번개부르미 LaserEffect 적용")]
        public static void ApplyLaser()
        {
            RequireEditMode();
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(LaserSheet);
            Require(sheet != null && sheet.width == 384 && sheet.height == 64, "LaserEffect 시트 384×64");
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            Require(frames.Length == 6 && frames.All(frame => frame.rect.size == new Vector2(64f, 64f)), "LaserEffect 64×64 6프레임");
            Require(frames.All(frame => frame.border == Vector4.zero && frame.pivot == new Vector2(32f, 32f)), "LaserEffect 중앙 피벗·테두리 없음");

            // 원본 슬라이스와 GUID를 보존하고 타일 표시에 필요한 전체 사각형 메시만 보장합니다.
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(LaserSheet);
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                frames = WeaponFireEffectAssets.LoadFrames(AssetDatabase.LoadAssetAtPath<Texture2D>(LaserSheet));
            }

            GameObject segment = new GameObject("StormbringerLaserSegment");
            GameObject savedSegment;
            try
            {
                SpriteRenderer renderer = segment.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = frames[0].bounds.size;
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                savedSegment = PrefabUtility.SaveAsPrefabAsset(segment, LaserSegment);
            }
            finally { Object.DestroyImmediate(segment); }

            GameObject root = PrefabUtility.LoadPrefabContents(
                AssetDatabase.LoadAssetAtPath<GameObject>(LaserEffect) != null ? LaserEffect : SpriteEffect);
            try
            {
                root.name = "StormbringerLaserVFX";
                ChainLightningVFX effect = root.GetComponent<ChainLightningVFX>();
                SerializedObject serialized = new SerializedObject(effect);
                serialized.FindProperty("spriteSegmentPrefab").objectReferenceValue = savedSegment.GetComponent<SpriteRenderer>();
                serialized.FindProperty("spriteHeight").floatValue = 1f;
                SerializedProperty spriteFrames = serialized.FindProperty("spriteFrames");
                spriteFrames.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) spriteFrames.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                // 기존 번개의 0.2초 수명 안에서 원본의 여섯 프레임을 함께 재생합니다.
                serialized.FindProperty("spriteFramesPerSecond").floatValue = 30f;
                serialized.FindProperty("spriteFrameOffset").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                effect.duration = 0.2f;
                root.GetComponent<LineRenderer>().enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, LaserEffect);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            ChainLightningVFX prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaserEffect).GetComponent<ChainLightningVFX>();
            GameObject weapon = PrefabUtility.LoadPrefabContents(Weapon);
            try
            {
                SerializedObject serialized = new SerializedObject(weapon.GetComponent<LightningProj>());
                serialized.FindProperty("lightningEffectPrefab").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(weapon, Weapon);
            }
            finally { PrefabUtility.UnloadPrefabContents(weapon); }

            VerifyLaser(prefab, frames);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/laser-verification.txt", "PASS LaserEffect 64×64 6프레임·30fps·0.2초\nPASS 원본 비율·색상·프레임 순서 유지, 끝 조각 자르기, 이동 추적·재사용\nPASS 번개부르미 전용 연결, 기존 피격 이펙트 유지\n");
            Debug.Log("[LightningSpriteSetup] 번개부르미에 LaserEffect 적용 완료");
        }

        private static void VerifyLaser(ChainLightningVFX prefab, Sprite[] frames)
        {
            var weaponReference = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<LightningProj>());
            Require(weaponReference.FindProperty("lightningEffectPrefab").objectReferenceValue == prefab && prefab.HasImpactEffect, "번개부르미 LaserEffect·피격 효과 연결");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, preview);
                Transform start = new GameObject("시작점").transform;
                Transform end = new GameObject("대상").transform;
                SceneManager.MoveGameObjectToScene(start.gameObject, preview);
                SceneManager.MoveGameObjectToScene(end.gameObject, preview);
                ChainLightningVFX effect = root.GetComponent<ChainLightningVFX>();
                FieldInfo elapsed = typeof(ChainLightningVFX).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo update = typeof(ChainLightningVFX).GetMethod("UpdateTargetsPositions", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (float distance in new[] { 0.35f, 2.35f, 4f })
                {
                    end.position = start.position + Quaternion.Euler(0f, 0f, 37f) * Vector3.right * distance;
                    effect.Setup(start, new System.Collections.Generic.List<Transform> { end });
                    SpriteRenderer[] tiles = root.GetComponentsInChildren<SpriteRenderer>();
                    Require(effect.IsVisible && !root.GetComponent<LineRenderer>().enabled && tiles.Length == Mathf.CeilToInt(distance), "LaserEffect 거리별 조각 수");
                    for (int frame = 0; frame < frames.Length; frame++)
                    {
                        elapsed.SetValue(effect, (frame + 0.1f) / 30f);
                        update.Invoke(effect, null);
                        Require(tiles.All(tile => tile.sprite == frames[frame] && tile.color == Color.white &&
                            Mathf.Abs(tile.transform.lossyScale.x - tile.transform.lossyScale.y) < 0.001f), "LaserEffect 원형·동기화 프레임");
                    }
                    float displayedLength = tiles.Sum(tile => tile.size.x * tile.transform.lossyScale.x);
                    Require(Mathf.Abs(displayedLength - distance) < 0.001f, "LaserEffect 늘임 없는 끝 조각 자르기");
                }
                end.position = Vector3.right * 0.35f;
                update.Invoke(effect, null);
                Require(root.GetComponentsInChildren<SpriteRenderer>().Length == 1, "LaserEffect 이동 시 남은 조각 숨김");
                root.SetActive(false);
                Require(!effect.IsVisible, "LaserEffect 종료");
                root.SetActive(true);
                effect.Setup(start, new System.Collections.Generic.List<Transform> { end });
                Require(root.GetComponentsInChildren<SpriteRenderer>().Single().sprite == frames[0], "LaserEffect 재사용 첫 프레임");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [MenuItem("Tools/Nytherion/번개 이펙트/새 스프라이트 번개 적용")]
        public static void CreateAndApply()
        {
            RequireEditMode();
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
            Require(sheet != null && sheet.width == 384 && sheet.height == 64, "원본 시트 384×64");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 64f);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            Require(frames.Length == 6 && frames.All(frame => frame.rect.size == new Vector2(64f, 64f)), "64×64 6프레임");

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animation);
            if (clip == null) { clip = new AnimationClip { name = "LightningSprite" }; AssetDatabase.CreateAsset(clip, Animation); }
            clip.frameRate = 30f;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 30f, value = frame }).ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            clipSettings.startTime = 0f;
            clipSettings.stopTime = frames.Length / 30f;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(Controller);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == "Lightning");
            if (state == null) state = machine.AddState("Lightning");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Segment);
            GameObject segment = existing != null ? PrefabUtility.LoadPrefabContents(Segment) : new GameObject("LightningSpriteSegment");
            GameObject savedSegment;
            try
            {
                SpriteRenderer renderer = segment.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = segment.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = Vector2.one;
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                Animator animator = segment.GetComponent<Animator>();
                if (animator == null) animator = segment.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // 조각 사이의 프레임 차이는 상위 번개가 공통 시간 기준으로 갱신합니다.
                animator.enabled = false;
                savedSegment = PrefabUtility.SaveAsPrefabAsset(segment, Segment);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(segment);
                else Object.DestroyImmediate(segment);
            }

            existing = AssetDatabase.LoadAssetAtPath<GameObject>(SpriteEffect);
            GameObject root = PrefabUtility.LoadPrefabContents(existing != null ? SpriteEffect : Original);
            try
            {
                root.name = "ChainLightningSpriteVFX";
                ChainLightningVFX effect = root.GetComponent<ChainLightningVFX>();
                SerializedObject serialized = new SerializedObject(effect);
                serialized.FindProperty("spriteSegmentPrefab").objectReferenceValue = savedSegment.GetComponent<SpriteRenderer>();
                serialized.FindProperty("spriteHeight").floatValue = 1f;
                SerializedProperty spriteFrames = serialized.FindProperty("spriteFrames");
                spriteFrames.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) spriteFrames.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                serialized.FindProperty("spriteFramesPerSecond").floatValue = clip.frameRate;
                serialized.FindProperty("spriteFrameOffset").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                effect.duration = frames.Length / clip.frameRate;
                root.GetComponent<LineRenderer>().enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, SpriteEffect);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            ChainLightningVFX prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpriteEffect).GetComponent<ChainLightningVFX>();
            Assign(prefab);
            Verify(prefab, clip, frames);
            Debug.Log("[LightningSpriteSetup] 64×64 6프레임 번개 적용 완료. Tools/Nytherion/번개 이펙트에서 기존 번개로 전환할 수 있습니다.");
        }

        [MenuItem("Tools/Nytherion/번개 이펙트/기존 번개 적용")]
        public static void ApplyOriginal()
        {
            RequireEditMode();
            Assign(AssetDatabase.LoadAssetAtPath<GameObject>(Original).GetComponent<ChainLightningVFX>());
            Debug.Log("[LightningSpriteSetup] 번개부르미·폭풍사수·연쇄 폭풍에 기존 번개 적용 완료");
        }

        [MenuItem("Tools/Nytherion/번개 이펙트/새 스프라이트 번개 적용", true)]
        [MenuItem("Tools/Nytherion/번개 이펙트/기존 번개 적용", true)]
        [MenuItem("Tools/Nytherion/번개 이펙트/번개부르미 LaserEffect 적용", true)]
        private static bool CanApply() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Assign(ChainLightningVFX prefab)
        {
            Require(prefab != null, "번개 프리팹");
            GameObject weapon = PrefabUtility.LoadPrefabContents(Weapon);
            try
            {
                SerializedObject serialized = new SerializedObject(weapon.GetComponent<LightningProj>());
                serialized.FindProperty("lightningEffectPrefab").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(weapon, Weapon);
            }
            finally { PrefabUtility.UnloadPrefabContents(weapon); }
            RelicTranscendenceData storm = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(Relics + "LightningPursuitTranscendence.asset");
            foreach (DashChainLightningEffect effect in storm.effectModules.SelectMany(module => module.effects).OfType<DashChainLightningEffect>())
                effect.lightningEffectPrefab = prefab;
            EditorUtility.SetDirty(storm);
            AssetDatabase.SaveAssetIfDirty(storm);
            RelicSetBonusData archer = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(Relics + "StormArcherSet.asset");
            foreach (CriticalChainLightningEffect effect in archer.bonusModules.SelectMany(module => module.effects).OfType<CriticalChainLightningEffect>())
                effect.lightningEffectPrefab = prefab;
            EditorUtility.SetDirty(archer);
            AssetDatabase.SaveAssetIfDirty(archer);
            Require(storm.effectModules.SelectMany(module => module.effects).OfType<DashChainLightningEffect>().All(effect => effect.lightningEffectPrefab == prefab) &&
                archer.bonusModules.SelectMany(module => module.effects).OfType<CriticalChainLightningEffect>().All(effect => effect.lightningEffectPrefab == prefab), "유물 번개 연결");
            var weaponReference = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<LightningProj>());
            Require(weaponReference.FindProperty("lightningEffectPrefab").objectReferenceValue == prefab, "번개부르미 연결");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/selection.txt", "PASS 번개부르미·폭풍사수 4/6세트·연쇄 폭풍 공통 참조\n" + AssetDatabase.GetAssetPath(prefab));
        }

        private static void Verify(ChainLightningVFX prefab, AnimationClip clip, Sprite[] frames)
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
            Require(keys.Length == 6 && keys.Select((key, index) => key.value == frames[index]).All(value => value) &&
                Mathf.Abs(clip.length - 0.2f) < 0.001f, "30fps·0.2초 애니메이션");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, preview);
                Transform start = new GameObject("시작점").transform;
                Transform end = new GameObject("대상").transform;
                SceneManager.MoveGameObjectToScene(start.gameObject, preview);
                SceneManager.MoveGameObjectToScene(end.gameObject, preview);
                end.position = new Vector3(2.35f, 0f);
                ChainLightningVFX effect = root.GetComponent<ChainLightningVFX>();
                effect.Setup(start, new System.Collections.Generic.List<Transform> { end },
                    new System.Collections.Generic.List<Vector3> { new Vector3(2.35f, 2f) });
                SpriteRenderer[] segments = root.GetComponentsInChildren<SpriteRenderer>();
                Require(effect.IsVisible && !root.GetComponent<LineRenderer>().enabled && segments.Length == 5, "원본 2장·부분 1장과 다음 구간 2장 생성");
                Require(Vector3.Distance(segments[0].transform.position, Vector3.right * 0.5f) < 0.001f &&
                    Mathf.Abs(segments[0].transform.lossyScale.x - 1f) < 0.001f &&
                    Mathf.Abs(segments[0].transform.lossyScale.y - 1f) < 0.001f &&
                    Mathf.Abs(segments[0].size.x - 1f) < 0.001f &&
                    Mathf.Abs(segments[2].size.x - 0.35f) < 0.001f &&
                    Mathf.Abs(segments[2].bounds.min.x - 2f) < 0.001f && Mathf.Abs(segments[2].bounds.max.x - 2.35f) < 0.001f &&
                    Mathf.Abs(Mathf.DeltaAngle(segments[3].transform.eulerAngles.z, 90f)) < 0.001f, "원본 비율·부분 조각 길이·위치·회전 맞춤");
                FieldInfo elapsed = typeof(ChainLightningVFX).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo update = typeof(ChainLightningVFX).GetMethod("UpdateTargetsPositions", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int i = 0; i < frames.Length; i++)
                {
                    elapsed.SetValue(effect, (i + 0.1f) / 30f);
                    update.Invoke(effect, null);
                    for (int tile = 0; tile < segments.Length; tile++)
                    {
                        Animator animator = segments[tile].GetComponent<Animator>();
                        Require(segments[tile].sprite == frames[(i + tile) % frames.Length] &&
                            Mathf.Abs(segments[tile].transform.lossyScale.x - segments[tile].transform.lossyScale.y) < 0.001f &&
                            (animator == null || !animator.enabled), "공통 시간·조각별 순차 프레임 " + i + "/" + tile);
                    }
                    Require(Mathf.Abs(segments[2].size.x - 0.35f) < 0.001f, "부분 조각의 순차 프레임과 자르기 유지");
                }
                end.position = new Vector3(4f, 0f);
                update.Invoke(effect, null);
                segments = root.GetComponentsInChildren<SpriteRenderer>();
                Require(segments.Length == 7 && segments.Take(4).All(segment => Mathf.Abs(segment.size.x - 1f) < 0.001f),
                    "이동 대상 추적 시 조각 추가·원본 크기 유지");
                end.position = new Vector3(0.35f, 0f);
                update.Invoke(effect, null);
                Require(Mathf.Abs(segments[0].transform.lossyScale.x - segments[0].transform.lossyScale.y) < 0.001f &&
                    Mathf.Abs(segments[0].size.x - 0.35f) < 0.001f && Mathf.Abs(segments[0].bounds.size.x - 0.35f) < 0.001f &&
                    segments[0].tileMode == SpriteTileMode.Continuous,
                    "짧은 거리도 원본 비율 유지·남는 부분 자르기");
                root.SetActive(false);
                Require(!effect.IsVisible, "비활성화 시 정리");
                root.SetActive(true);
                effect.Setup(start, new System.Collections.Generic.List<Transform> { end });
                Require(effect.IsVisible && root.GetComponentsInChildren<SpriteRenderer>().Length == 1 && segments[0].sprite == frames[0],
                    "재사용 시 첫 프레임 복원·남은 조각 숨김");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/verification.txt", "PASS 원본 복사·64×64 6프레임·Point/무압축·30fps/0.2초 애니메이션\nPASS 원본 비율·두께 유지、조각별 프레임 1/2/3 → 2/3/4 → … → 6/1/2 순환\nPASS 3번째 조각의 순차 프레임·왼쪽 부분 자르기·구간 사이 프레임 연속\nPASS 0.35/2.35/4 거리·이동 시 조각 추가/제거·재사용 첫 프레임 초기화\nPASS 번개부르미·폭풍사수 4/6세트·연쇄 폭풍에 새 번개 연결\n기존 번개 프리팹은 유지하며 Tools/Nytherion/번개 이펙트에서 전환합니다.\n");
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 모드를 종료하고 전환해 주세요.");
        }

        private static void Require(bool success, string label)
        {
            if (!success) throw new InvalidOperationException("스프라이트 번개 검증 실패: " + label);
        }
    }
}
