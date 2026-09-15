using System;
using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    public static class LayLaserWeaponSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/LayLaser.asset";
        public const string WeaponPath = "Assets/Prefabs/Gameplay/Combat/Weapons/LayLaser.prefab";
        public const string BeamPath = "Assets/Prefabs/Gameplay/Combat/Proj/Player_LayLaser.prefab";
        public const string IdleClipPath = "Assets/Nytherion/Art/Combat/Weapons/Animations/LayLaser/Idle.anim";
        public const string ChargeClipPath = "Assets/Nytherion/Art/Combat/Weapons/Animations/LayLaser/Charge.anim";
        public const string FullChargeClipPath = "Assets/Nytherion/Art/Combat/Weapons/Animations/LayLaser/FullCharge.anim";
        public const string ControllerPath = "Assets/Nytherion/Art/Combat/Weapons/Animations/LayLaser/LayLaser.controller";
        private const string DatabasePath = "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset";
        private const string AnimationDirectory = "Assets/Nytherion/Art/Combat/Weapons/Animations/LayLaser";

        [MenuItem("Tools/Nytherion/Lay Laser/Create Assets %#&l")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("레이 레이저 에셋은 편집 모드에서 생성해 주세요.");

            Sprite weaponSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Nytherion/Art/Combat/Weapons/Sprites/LayLaser.png");
            Sprite[] chargeFrames = LoadFrames("EnergyCharge", 19);
            Sprite[] beamFrames = LoadFrames("LayLaserEffect", 8);
            Sprite[] startEndFrames = LoadFrames("LayLaserStartEnd", 8);
            ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DatabasePath);
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (weaponSprite == null || database == null || material == null ||
                LayerMask.NameToLayer("Enemy") < 0 || LayerMask.NameToLayer("Wall") < 0 ||
                LayerMask.NameToLayer("Obstacle") < 0)
                throw new InvalidOperationException("레이 레이저 원본 이미지, 데이터베이스 또는 레이어가 없습니다.");

            LayLaserWeaponData data = LoadExisting<LayLaserWeaponData>(DataPath);
            GameObject weaponPrefab = LoadExisting<GameObject>(WeaponPath);
            GameObject beamPrefab = LoadExisting<GameObject>(BeamPath);
            if (weaponPrefab != null && weaponPrefab.GetComponent<LayLaserWeapon>() == null ||
                beamPrefab != null && beamPrefab.GetComponent<LayLaserBeam>() == null)
                throw new InvalidOperationException("기존 경로의 프리팹이 레이 레이저 타입과 다릅니다.");

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<LayLaserWeaponData>();
                data.itemName_KR = "레이 레이저";
                data.itemName_EN = "Lay Laser";
                data.description_KR = "공격 버튼을 눌러 4단계로 충전합니다. 버튼을 놓으면 단계에 따라 길고 굵어지는 광선을 발사해 적에게 주기적으로 피해를 줍니다.";
                data.description_EN = "Hold to charge through four stages. Release to fire a beam that grows longer and wider with charge and deals periodic damage.";
                data.weaponType = WeaponType.Ranged;
                data.equipmentType = EquipmentType.Weapon;
                data.weaponSprite = weaponSprite;
                data.icon = weaponSprite;
                data.chargeFrames = chargeFrames;
                data.beamFrames = beamFrames;
                data.startEndFrames = startEndFrames;
                data.damage = 4f;
                data.range = 10f;
                data.cooldown = 0.6f;
                data.maxChargeTime = 1.2f;
                data.chargeThresholdTime = 0f;
                data.visualScale = 0.7f;
                data.spriteRotationOffset = 0f;
                data.firePointOffset = new Vector3(1.15f, 0f, 0f);
                data.useStaffRecoil = false;
                data.isArchivable = false;
                data.projectileSpeed = 0f;
                data.isStackable = false;
                data.maxStack = 1;
                data.baseValue = 150;
                data.targetLayers = LayerMask.GetMask("Enemy");
                data.obstructionLayers = LayerMask.GetMask("Wall", "Obstacle");
                SerializedObject serialized = new SerializedObject(data);
                serialized.FindProperty("uniqueID").stringValue = Guid.NewGuid().ToString();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            data.chargeFrames = chargeFrames;
            data.beamFrames = beamFrames;
            data.startEndFrames = startEndFrames;

            AnimatorController controller = CreateAnimationAssets(weaponSprite, chargeFrames);
            if (beamPrefab == null) beamPrefab = CreateBeam(material);
            else beamPrefab = ConfigureBeamPrefab(material);
            if (weaponPrefab == null) weaponPrefab = CreateWeapon(data, material, controller);
            else weaponPrefab = ConfigureWeaponPrefab(data, material, controller);
            if (data.weaponPrefab == null) data.weaponPrefab = weaponPrefab.GetComponent<LayLaserWeapon>();
            if (data.projectilePrefab == null) data.projectilePrefab = beamPrefab;
            data.animatorController = controller;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            if (database.allItems == null) database.allItems = new List<ItemData>();
            if (!database.allItems.Contains(data))
            {
                Undo.RecordObject(database, "레이 레이저 등록");
                database.allItems.Add(data);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssetIfDirty(database);
            }
            Selection.activeObject = data;
            Debug.Log("[LayLaserWeaponSetup] 레이 레이저 데이터, 프리팹 및 아이템 데이터베이스 연결 완료.", data);
        }

        private static Sprite[] LoadFrames(string name, int count)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(
                $"Assets/Nytherion/Art/Combat/VFX/Sprites/{name}.png").OfType<Sprite>().ToArray();
            Sprite[] frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = sprites.SingleOrDefault(sprite => sprite.name == $"{name}_{i}");
                if (frames[i] == null) throw new InvalidOperationException($"스프라이트 누락: {name}_{i}");
            }
            if (sprites.Length != count) throw new InvalidOperationException($"{name} 프레임 수를 확인해 주세요: {sprites.Length}");
            return frames;
        }

        private static T LoadExisting<T>(string path) where T : UnityEngine.Object
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && !(asset is T)) throw new InvalidOperationException($"기존 에셋 타입 불일치: {path}");
            return asset as T;
        }

        private static AnimatorController CreateAnimationAssets(Sprite weaponSprite, Sprite[] chargeFrames)
        {
            EnsureFolder(AnimationDirectory);
            AnimationClip idle = CreateOrUpdateClip(IdleClipPath, new[] { weaponSprite }, false);
            AnimationClip charge = CreateOrUpdateClip(ChargeClipPath,
                chargeFrames.Take(LayLaserWeaponData.ChargeStartupFrameCount).ToArray(), false);
            AnimationClip fullCharge = CreateOrUpdateClip(FullChargeClipPath,
                chargeFrames.Skip(LayLaserWeaponData.FullChargeStartFrame)
                    .Take(LayLaserWeaponData.FullChargeFrameCount).ToArray(), true);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.parameters = new[]
            {
                new AnimatorControllerParameter { name = "Charging", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "ChargeSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f },
                new AnimatorControllerParameter { name = "FullChargeSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f }
            };

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in stateMachine.states)
                stateMachine.RemoveState(child.state);
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
                stateMachine.RemoveAnyStateTransition(transition);
            foreach (AnimatorTransition transition in stateMachine.entryTransitions)
                stateMachine.RemoveEntryTransition(transition);

            AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(250f, 100f));
            idleState.motion = idle;
            AnimatorState chargeState = stateMachine.AddState("Charge", new Vector3(500f, 100f));
            chargeState.motion = charge;
            chargeState.speedParameterActive = true;
            chargeState.speedParameter = "ChargeSpeed";
            AnimatorState fullChargeState = stateMachine.AddState("FullCharge", new Vector3(750f, 100f));
            fullChargeState.motion = fullCharge;
            fullChargeState.speedParameterActive = true;
            fullChargeState.speedParameter = "FullChargeSpeed";
            stateMachine.defaultState = idleState;

            AnimatorStateTransition startCharging = idleState.AddTransition(chargeState);
            ConfigureTransition(startCharging, false);
            startCharging.AddCondition(AnimatorConditionMode.If, 0f, "Charging");

            AnimatorStateTransition cancelCharging = chargeState.AddTransition(idleState);
            ConfigureTransition(cancelCharging, false);
            cancelCharging.AddCondition(AnimatorConditionMode.IfNot, 0f, "Charging");

            AnimatorStateTransition fullyCharged = chargeState.AddTransition(fullChargeState);
            ConfigureTransition(fullyCharged, true);

            AnimatorStateTransition finishCharging = fullChargeState.AddTransition(idleState);
            ConfigureTransition(finishCharging, false);
            finishCharging.AddCondition(AnimatorConditionMode.IfNot, 0f, "Charging");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(idle);
            AssetDatabase.SaveAssetIfDirty(charge);
            AssetDatabase.SaveAssetIfDirty(fullCharge);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        private static AnimationClip CreateOrUpdateClip(string path, Sprite[] frames, bool loop)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
            clip.frameRate = LayLaserWeaponData.ChargeAnimationFramesPerSecond;
            clip.ClearCurves();
            List<ObjectReferenceKeyframe> keys = new List<ObjectReferenceKeyframe>(frames.Length);
            for (int i = 0; i < frames.Length; i++)
            {
                keys.Add(new ObjectReferenceKeyframe
                {
                    time = i / LayLaserWeaponData.ChargeAnimationFramesPerSecond,
                    value = frames[i]
                });
            }
            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty,
                typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void ConfigureTransition(AnimatorStateTransition transition, bool hasExitTime)
        {
            transition.duration = 0f;
            transition.hasFixedDuration = true;
            transition.hasExitTime = hasExitTime;
            transition.exitTime = hasExitTime ? 1f : 0f;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException($"애니메이션 폴더 경로가 올바르지 않습니다: {path}");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static GameObject CreateWeapon(LayLaserWeaponData data, Material material, AnimatorController controller)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("LayLaser");
                SceneManager.MoveGameObjectToScene(root, scene);
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = data.weaponSprite;
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 1;
                Transform muzzle = new GameObject("FirePoint").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = data.firePointOffset;
                Animator animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                LayLaserWeapon weapon = root.AddComponent<LayLaserWeapon>();
                weapon.weaponData = data;
                weapon.firePoint = muzzle;
                SerializedObject serialized = new SerializedObject(weapon);
                serialized.FindProperty("weaponRenderer").objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return SavePrefab(root, WeaponPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static GameObject ConfigureWeaponPrefab(LayLaserWeaponData data, Material material,
            AnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(WeaponPath);
            try
            {
                LayLaserWeapon weapon = root.GetComponent<LayLaserWeapon>();
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (weapon == null || renderer == null)
                    throw new InvalidOperationException("LayLaser 프리팹의 본체 컴포넌트를 확인해 주세요.");

                renderer.sprite = data.weaponSprite;
                renderer.sharedMaterial = material;
                Transform firePoint = root.transform.Find("FirePoint");
                if (firePoint == null)
                    throw new InvalidOperationException("LayLaser 프리팹에 FirePoint가 없습니다.");
                Transform oldCharge = firePoint.Find("EnergyCharge");
                if (oldCharge != null) UnityEngine.Object.DestroyImmediate(oldCharge.gameObject);

                Animator animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                weapon.weaponData = data;
                weapon.firePoint = firePoint;
                SerializedObject serialized = new SerializedObject(weapon);
                serialized.FindProperty("weaponRenderer").objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, WeaponPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject CreateBeam(Material material)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("Player_LayLaser");
                SceneManager.MoveGameObjectToScene(root, scene);
                LayLaserBeam beam = root.AddComponent<LayLaserBeam>();
                Transform visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                SpriteRenderer renderer = visual.gameObject.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 2;
                SpriteRenderer startRenderer = CreateEndpointRenderer(root.transform, "StartEffect", material);
                SpriteRenderer endRenderer = CreateEndpointRenderer(root.transform, "EndEffect", material);
                SerializedObject serialized = new SerializedObject(beam);
                serialized.FindProperty("beamRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("startEffectRenderer").objectReferenceValue = startRenderer;
                serialized.FindProperty("endEffectRenderer").objectReferenceValue = endRenderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return SavePrefab(root, BeamPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static GameObject ConfigureBeamPrefab(Material material)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BeamPath);
            try
            {
                LayLaserBeam beam = root.GetComponent<LayLaserBeam>();
                Transform visual = root.transform.Find("Visual");
                SpriteRenderer renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                if (beam == null || renderer == null)
                    throw new InvalidOperationException("LayLaser 광선 프리팹의 본체 컴포넌트를 확인해 주세요.");

                renderer.sharedMaterial = material;
                renderer.sortingOrder = 2;
                SpriteRenderer startRenderer = CreateEndpointRenderer(root.transform, "StartEffect", material);
                SpriteRenderer endRenderer = CreateEndpointRenderer(root.transform, "EndEffect", material);
                SerializedObject serialized = new SerializedObject(beam);
                serialized.FindProperty("beamRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("startEffectRenderer").objectReferenceValue = startRenderer;
                serialized.FindProperty("endEffectRenderer").objectReferenceValue = endRenderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, BeamPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static SpriteRenderer CreateEndpointRenderer(Transform parent, string name, Material material)
        {
            Transform endpoint = parent.Find(name);
            if (endpoint == null)
            {
                endpoint = new GameObject(name).transform;
                endpoint.SetParent(parent, false);
            }
            SpriteRenderer renderer = endpoint.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = endpoint.gameObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 3;
            renderer.enabled = false;
            return renderer;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || prefab == null) throw new InvalidOperationException($"프리팹 저장 실패: {path}");
            return prefab;
        }
    }
}
