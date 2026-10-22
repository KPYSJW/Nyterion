using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    public static class PyrofallChargeEffectSetup
    {
        public const string ImagePath = "Assets/Nytherion/Art/Combat/VFX/Sprites/PyrofallCharging.png";
        public const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/PyrofallCharging.prefab";
        private const string AnimationFolder = "Assets/Nytherion/Art/Combat/VFX/Animations/Weapons/PyrofallCharging";

        [MenuItem("Tools/Nytherion/Pyrofall/Create Charging Effect")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 차징 이펙트를 생성해 주세요.");
            AssetDatabase.ImportAsset(ImagePath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
            if (sheet == null || sheet.width != 384 || sheet.height != 64)
                throw new InvalidOperationException("차징 이미지의 64×64, 6프레임 구성을 확인해 주세요.");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 32f);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(PyrofallWeaponSetup.DataPath);
            if (data == null || frames.Length != 6)
                throw new InvalidOperationException("파이로폴 데이터 또는 차징 프레임이 누락되었습니다.");
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
                AssetDatabase.CreateFolder("Assets/Nytherion/Art/Combat/VFX/Animations/Weapons", "PyrofallCharging");
            string clipPath = AnimationFolder + "/PyrofallCharging.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            clip.frameRate = 12f;
            clip.wrapMode = WrapMode.Loop;
            // 마지막 프레임도 같은 시간 동안 유지한 후 첫 프레임으로 돌아갑니다.
            var keys = new ObjectReferenceKeyframe[7];
            for (int i = 0; i < 6; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = frames[i] };
            keys[6] = new ObjectReferenceKeyframe { time = 0.5f, value = frames[0] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("Visual", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.startTime = 0f;
            settings.stopTime = 0.5f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            string controllerPath = AnimationFolder + "/PyrofallCharging.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Select(entry => entry.state).FirstOrDefault(entry => entry.name == "Charging");
            if (state == null) state = machine.AddState("Charging");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("PyrofallCharging");
            try
            {
                Transform visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    visual = new GameObject("Visual").transform;
                    visual.SetParent(root.transform, false);
                }
                visual.localPosition = new Vector3(0f, 0.68f, 0f);
                visual.localScale = Vector3.one * 0.75f;
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                renderer.sortingOrder = 2;
                Animator animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                data.chargeEffectPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log("[PyrofallCharging] 6프레임 차징 애니메이션 생성 및 WeaponData 연결 완료.");
        }

        [MenuItem("Tools/Nytherion/Pyrofall/Verify Charging Effect")]
        public static void Verify()
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(PyrofallWeaponSetup.DataPath);
            Require(AssetDatabase.GetAssetPath(data.chargeEffectPrefab) == PrefabPath, "차징 프리팹 연결");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "/PyrofallCharging.anim");
            Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime &&
                Mathf.Approximately(clip.length, 0.5f) && clip.frameRate == 12f, "12 FPS·0.5초 반복");
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath));
            var keys = AnimationUtility.GetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("Visual", typeof(SpriteRenderer), "m_Sprite"));
            Require(keys.Take(6).Select(key => key.value).SequenceEqual(frames), "6프레임 순서");
            Scene scene = EditorSceneManager.NewPreviewScene();
            ObjectPoolManager previousPool = ObjectPoolManager.Instance;
            PropertyInfo poolInstance = typeof(ObjectPoolManager).GetProperty("Instance");
            try
            {
                GameObject root = new GameObject("차징 이펙트 검증");
                SceneManager.MoveGameObjectToScene(root, scene);
                ObjectPoolManager pool = root.AddComponent<ObjectPoolManager>();
                poolInstance.SetValue(null, pool);
                GameObject initial = pool.SpawnFromPool(data.chargeEffectPrefab, Vector3.zero, Quaternion.identity, 1);
                pool.ReturnToPool(data.chargeEffectPrefab.name, initial);
                PlayerCombat combat = root.AddComponent<PlayerCombat>();
                GameObject weapon = UnityEngine.Object.Instantiate(data.weaponPrefab.gameObject, root.transform);
                WeaponData testData = UnityEngine.Object.Instantiate(data);
                try
                {
                    // 낙하 공격을 생성하지 않고 실제 차징 종료 경로를 검증합니다.
                    testData.projectilePrefab = null;
                    combat.currentWeapon = weapon.GetComponent<WeaponBase>();
                    combat.currentWeapon.weaponData = testData;
                    testData.chargeThresholdTime = 0.15f;
                    testData.maxChargeTime = 1f;
                    Call(combat, "BeginGenericCharge");
                    Advance(combat, 0.149f);
                    Require(!combat.IsGenericCharging && Effect(combat) == null && combat.GenericChargePercent == 0f,
                        "0.15초 미만에는 차징 UI·이펙트·충전량 없음");
                    combat.currentWeapon.AttackWithGenericCharge(Vector2.right, Vector3.zero, 1f);
                    Call(combat, "FireGenericCharge");
                    Require(Mathf.Approximately(combat.currentWeapon.CurrentDamageMultiplier, 1f),
                        "짧은 클릭은 기존 차징 배율을 지우고 일반 공격");
                    Call(combat, "BeginGenericCharge");
                    Advance(combat, 0.15f);
                    Require(combat.IsGenericCharging && combat.GenericChargePercent == 0f,
                        "기준 시점에 충전량 0부터 차징 시작");
                    GameObject effect = (GameObject)typeof(PlayerCombat).GetField("genericChargeEffect",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(combat);
                    Require(effect != null && effect.activeSelf, "차징 시작 시 표시");
                    Animator animator = effect.GetComponent<Animator>();
                    SpriteRenderer renderer = effect.GetComponentInChildren<SpriteRenderer>();
                    Require(renderer.sprite == frames[0], "첫 프레임부터 재생");
                    animator.Update(1f / 12f + 0.001f);
                    Require(renderer.sprite == frames[1], "다음 프레임 재생");
                    animator.Update(0.5f);
                    Require(renderer.sprite == frames[1], "한 주기 이후 반복");
                    weapon.transform.localScale = new Vector3(-1f, 1f, 1f);
                    Require(effect.transform.parent == weapon.transform && effect.transform.lossyScale.x < 0f,
                        "무기 위치와 좌우 반전 추적");
                    Call(combat, "CancelGenericCharge");
                    Require(!effect.activeSelf, "차징 취소 후 풀 반환");
                    Call(combat, "BeginGenericCharge");
                    Advance(combat, 0.65f);
                    Require(Mathf.Approximately(combat.GenericChargePercent, 0.5f), "기준 시간 제외 후 충전량 계산");
                    Require(effect.activeSelf && renderer.sprite == frames[0], "풀 재사용 시 첫 프레임 복원");
                    Call(combat, "FireGenericCharge");
                    Require(!effect.activeSelf && Mathf.Approximately(combat.currentWeapon.CurrentDamageMultiplier, 1.5f),
                        "차징 발사 배율 및 연출 종료");
                    Call(combat, "BeginGenericCharge");
                    Advance(combat, 0.15f);
                    Call(combat, "OnDisable");
                    Require(!effect.activeSelf && !combat.IsGenericCharging, "플레이어 비활성화 시 연출 종료");
                    testData.chargeThresholdTime = 0f;
                    Call(combat, "BeginGenericCharge");
                    Require(combat.IsGenericCharging && effect.activeSelf, "기준 시간이 0이면 즉시 차징");
                    Call(combat, "CancelGenericCharge");
                    testData.chargeThresholdTime = 0.15f;
                    Call(combat, "BeginGenericCharge");
                    Call(combat, "OnDisable");
                    Require(!combat.IsGenericCharging && Effect(combat) == null, "차징 판정 대기 중 비활성화 취소");
                }
                finally { UnityEngine.Object.DestroyImmediate(testData); }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                poolInstance.SetValue(null, previousPool);
            }
            Directory.CreateDirectory("output/pyrofall-charging");
            File.WriteAllText("output/pyrofall-charging/verification.txt",
                "PASS 일반 클릭·0.15초 경계·기준 시간 제외·0초 즉시 차징\nPASS 6프레임·12 FPS·반복 재생\nPASS 차징 시작·취소·발사·비활성화\nPASS 좌우 반전·풀 반환·재사용\n");
            Debug.Log("[PyrofallChargingVerification] PASS");
        }

        private static GameObject Effect(PlayerCombat combat) => (GameObject)typeof(PlayerCombat)
            .GetField("genericChargeEffect", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(combat);

        private static void Advance(PlayerCombat combat, float heldTime) => typeof(PlayerCombat)
            .GetMethod("UpdateGenericChargeProgress", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(combat, new object[] { heldTime });

        private static void Call(PlayerCombat target, string method) => typeof(PlayerCombat)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
