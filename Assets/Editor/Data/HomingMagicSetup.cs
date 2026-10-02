using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>기존 정령의 인도 무기와 투사체에 곡선 유도 설정 및 임시 4프레임 아트를 연결합니다.</summary>
    [InitializeOnLoad]
    public static class HomingMagicSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/SpiritsGuidance.asset";
        public const string ProjectilePath = "Assets/Prefabs/Gameplay/Combat/Proj/Spirits'GuidanceProj.prefab";
        public const string AnimationFolder = "Assets/Nytherion/Art/Combat/VFX/Animations/Homing";
        public const string ClipPath = AnimationFolder + "/HomingLoop.anim";
        public const string ControllerPath = AnimationFolder + "/Homing.controller";
        public const string Output = "output/homing-magic";

        static HomingMagicSetup() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (File.Exists(Output + "/balance.request"))
                {
                    File.Delete(Output + "/balance.request");
                    UpdateBalanceDescription();
                }
                if (File.Exists(Output + "/setup.request"))
                {
                    File.Delete(Output + "/setup.request");
                    CreateAssets();
                }
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Homing Magic/Apply To Spirits Guidance")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath("Assets/Nytherion/Art/Combat/VFX/Sprites/Homing.png")
                .OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
            if (data == null || frames.Length != 4 || frames.Any(frame => frame.rect.width != 32 || frame.rect.height != 32))
                throw new InvalidOperationException("기존 무기 데이터 또는 Homing.png의 32×32 4프레임을 확인해 주세요.");
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
                AssetDatabase.CreateFolder("Assets/Nytherion/Art/Combat/VFX/Animations", "Homing");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "HomingLoop" };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            clip.ClearCurves();
            clip.frameRate = 12f;
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i % 4] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.FirstOrDefault(child => child.state.name == "HomingLoop").state;
            if (state == null) state = machine.AddState("HomingLoop");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject root = PrefabUtility.LoadPrefabContents(ProjectilePath);
            try
            {
                root.GetComponent<SpriteRenderer>().sprite = frames[0];
                Animator animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.keepAnimatorStateOnDisable = false;
                Rigidbody2D body = root.GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                root.GetComponent<CircleCollider2D>().radius = 0.22f;
                var movement = new SerializedObject(root.GetComponent<HomingProj>());
                movement.FindProperty("rotateSpeed").floatValue = 180f;
                movement.FindProperty("initialStraightDuration").floatValue = 0.15f;
                movement.FindProperty("trackingRadius").floatValue = 10f;
                movement.ApplyModifiedPropertiesWithoutUndo();
                var lifetime = new SerializedObject(root.GetComponent<AutoReturnToPool>());
                lifetime.FindProperty("returnDelay").floatValue = 4f;
                lifetime.ApplyModifiedPropertiesWithoutUndo();
                var renderer = new SerializedObject(root.GetComponent<SpriteRenderer>());
                renderer.FindProperty("m_Sprite").objectReferenceValue = frames[0];
                renderer.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            data.hasHomingProjectiles = true;
            data.useHomingLaunchAngles = true;
            data.homingLaunchAngles = new[] { 30f, 15f, 0f, -15f, -30f };
            data.homingTurnSpeed = 180f;
            data.homingSearchRadius = 10f;
            data.homingLaunchDuration = 0.15f;
            data.homingLifetime = 4f;
            data.projectileSpeed = 6f;
            SetBalanceDescription(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/setup.txt", "PASS: 기존 무기/투사체 GUID 유지, 기본 1발 및 추가 탄 각도·유도 설정, Homing 4프레임 12fps 루프 연결");
            Debug.Log("[HomingMagicSetup] 정령의 인도 곡선 유도 마법탄 연결 완료", data);
        }

        private static void SetBalanceDescription(WeaponData data)
        {
            data.description_KR = "마법탄 한 발을 발사해 선택한 적을 추적합니다. 투사체 증가 효과에 따라 발사 수가 늘어납니다.";
            data.description_EN = "Fires one homing magic bolt. Extra projectile effects increase the number of bolts.";
        }

        private static void UpdateBalanceDescription()
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
            if (data == null) throw new InvalidOperationException("정령의 인도 무기 데이터 누락");
            SetBalanceDescription(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            File.WriteAllText(Output + "/balance.txt", "PASS: 기본 1발 및 투사체 증가 효과 설명 반영, 기존 아트/무기 설정 유지");
        }
    }
}
