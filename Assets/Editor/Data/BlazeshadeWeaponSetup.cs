using System;
using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Gacha;
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
    public static class BlazeshadeWeaponSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/Blazeshade.asset";
        public const string WeaponPath = "Assets/Prefabs/Gameplay/Combat/Weapons/Generated/Blazeshade.prefab";
        public const string AuraPath = "Assets/Prefabs/Gameplay/Combat/VFX/BlazeshadeAura.prefab";
        public const string ClipPath = "Assets/Nytherion/Art/Combat/Weapons/Animations/Blazeshade/FireShieldLoop.anim";
        public const string ControllerPath = "Assets/Nytherion/Art/Combat/Weapons/Animations/Blazeshade/BlazeshadeAura.controller";

        private const string WeaponSpritePath = "Assets/Nytherion/Art/Combat/Weapons/Sprites/Blazeshade.png";
        private const string IconSpritePath = "Assets/Nytherion/Art/Combat/Weapons/Sprites/Blazeshade_Icon.png";
        private const string FireShieldPath = "Assets/Nytherion/Art/UI/Legacy/Fire_Shield_0100.png";
        private const string DatabasePath = "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset";
        private const string RarePoolPath = "Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Weapon/Rare_Weapon.asset";
        private const string AnimationDirectory = "Assets/Nytherion/Art/Combat/Weapons/Animations/Blazeshade";

        [MenuItem("Tools/Nytherion/Blazeshade/Create Assets %#&b")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("블레이즈셰이드 에셋은 편집 모드에서 생성해 주세요.");
            }

            Sprite weaponSprite = ConfigureSingleSprite(WeaponSpritePath);
            Sprite iconSprite = ConfigureSingleSprite(IconSpritePath);
            Sprite[] auraFrames = LoadAuraFrames();
            ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DatabasePath);
            GachaPoolSO rarePool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(RarePoolPath);
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            if (weaponSprite == null || iconSprite == null || auraFrames.Length != 30 ||
                database == null || rarePool == null || material == null || LayerMask.NameToLayer("Enemy") < 0)
            {
                throw new InvalidOperationException("블레이즈셰이드 원본 이미지, 데이터베이스, 뽑기 풀 또는 Enemy 레이어를 확인해 주세요.");
            }

            WeaponData data = LoadExisting<WeaponData>(DataPath);
            if (data == null)
            {
                data = CreateData(weaponSprite, iconSprite);
            }

            AnimatorController controller = CreateAnimationAssets(auraFrames);
            GameObject auraPrefab = CreateOrUpdateAuraPrefab(auraFrames[0], material, controller);
            GameObject weaponPrefab = CreateOrUpdateWeaponPrefab(data, weaponSprite, auraPrefab, material);

            data.weaponSprite = weaponSprite;
            data.icon = iconSprite;
            data.weaponPrefab = weaponPrefab.GetComponent<BlazeshadeWeapon>();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            RegisterInDatabase(database, data);
            RegisterInRarePool(rarePool, data);
            AssetDatabase.SaveAssets();
            Selection.activeObject = data;
            Debug.Log("[BlazeshadeWeaponSetup] 블레이즈셰이드 데이터, 프리팹, 오라 애니메이션 및 뽑기 풀 연결 완료.", data);
        }

        private static WeaponData CreateData(Sprite weaponSprite, Sprite iconSprite)
        {
            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            data.itemName_KR = "블레이즈셰이드";
            data.itemName_EN = "Blazeshade";
            data.description_KR = "공격하지 않는 대신, 장착 중 플레이어 주위에 불꽃 고리를 펼쳐 범위 안의 적에게 1초마다 피해를 줍니다.";
            data.description_EN = "Instead of attacking, surrounds the player with a ring of fire that damages nearby enemies every second.";
            data.isStackable = false;
            data.maxStack = 1;
            data.baseValue = 180;
            data.equipmentType = EquipmentType.Weapon;
            data.traits = new List<EquipmentTrait> { EquipmentTrait.Fire };
            data.statModifiers = new List<Nytherion.Core.Data.StatModifier>();
            data.damage = 10f;
            data.range = 2.75f;
            data.cooldown = 1f;
            data.weaponType = WeaponType.Ranged;
            data.weaponSprite = weaponSprite;
            data.icon = iconSprite;
            data.spriteRotationOffset = -10f;
            data.visualPositionOffset = new Vector3(0.2f, -0.05f, 0f);
            data.visualScale = 1.25f;
            data.useStaffRecoil = false;
            data.projectileSpeed = 0f;
            data.isArchivable = false;

            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("uniqueID").stringValue = Guid.NewGuid().ToString();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(data, DataPath);
            return data;
        }

        private static Sprite ConfigureSingleSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite[] LoadAuraFrames()
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(FireShieldPath)
                .OfType<Sprite>()
                .ToArray();
            Sprite[] frames = new Sprite[30];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = sprites.SingleOrDefault(sprite => sprite.name == $"Fire_Shield_0100_{i}");
                if (frames[i] == null)
                {
                    throw new InvalidOperationException($"Fire_Shield 프레임 누락: Fire_Shield_0100_{i}");
                }
            }
            return frames;
        }

        private static AnimatorController CreateAnimationAssets(Sprite[] frames)
        {
            EnsureFolder(AnimationDirectory);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, ClipPath);
            }

            clip.name = "FireShieldLoop";
            clip.frameRate = 12f;
            clip.ClearCurves();
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = frames[i]
                };
            }

            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                stateMachine.RemoveState(child.state);
            }

            AnimatorState loopState = stateMachine.AddState("FireShieldLoop", new Vector3(300f, 100f));
            loopState.motion = clip;
            stateMachine.defaultState = loopState;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        private static GameObject CreateOrUpdateAuraPrefab(Sprite firstFrame, Material material, AnimatorController controller)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(AuraPath);
            if (existing == null)
            {
                Scene scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject("BlazeshadeAura");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    ConfigureAuraObject(root, firstFrame, material, controller);
                    return SavePrefab(root, AuraPath);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(AuraPath);
            try
            {
                ConfigureAuraObject(contents, firstFrame, material, controller);
                return PrefabUtility.SaveAsPrefabAsset(contents, AuraPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureAuraObject(GameObject root, Sprite firstFrame, Material material, AnimatorController controller)
        {
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = root.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = firstFrame;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = -2;
            renderer.color = new Color(1f, 1f, 1f, 0.8f);

            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;
        }

        private static GameObject CreateOrUpdateWeaponPrefab(
            WeaponData data,
            Sprite weaponSprite,
            GameObject auraPrefab,
            Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPath);
            if (existing == null)
            {
                Scene scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject("Blazeshade");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    ConfigureWeaponObject(root, data, weaponSprite, auraPrefab, material);
                    return SavePrefab(root, WeaponPath);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(WeaponPath);
            try
            {
                ConfigureWeaponObject(contents, data, weaponSprite, auraPrefab, material);
                return PrefabUtility.SaveAsPrefabAsset(contents, WeaponPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureWeaponObject(
            GameObject root,
            WeaponData data,
            Sprite weaponSprite,
            GameObject auraPrefab,
            Material material)
        {
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = root.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = weaponSprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 1;

            BlazeshadeWeapon weapon = root.GetComponent<BlazeshadeWeapon>();
            if (weapon == null)
            {
                weapon = root.AddComponent<BlazeshadeWeapon>();
            }
            weapon.weaponData = data;

            SerializedObject serialized = new SerializedObject(weapon);
            serialized.FindProperty("auraVisualPrefab").objectReferenceValue = auraPrefab;
            serialized.FindProperty("enemyLayers").intValue = LayerMask.GetMask("Enemy");
            serialized.FindProperty("overlapBufferSize").intValue = 64;
            serialized.FindProperty("auraSortingOrderOffset").intValue = -2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterInDatabase(ItemDatabaseSO database, WeaponData data)
        {
            if (database.allItems == null)
            {
                database.allItems = new List<ItemData>();
            }
            if (database.allItems.Contains(data))
            {
                return;
            }

            database.allItems.Add(data);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);
        }

        private static void RegisterInRarePool(GachaPoolSO rarePool, WeaponData data)
        {
            if (rarePool.items == null)
            {
                rarePool.items = new List<GachaItemRate>();
            }
            if (rarePool.items.Any(entry => entry.item == data))
            {
                return;
            }

            rarePool.items.Add(new GachaItemRate { item = data, weight = 100 });
            EditorUtility.SetDirty(rarePool);
            AssetDatabase.SaveAssetIfDirty(rarePool);
        }

        private static T LoadExisting<T>(string path) where T : UnityEngine.Object
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && !(asset is T))
            {
                throw new InvalidOperationException($"기존 에셋 타입 불일치: {path}");
            }
            return asset as T;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException($"폴더 경로가 올바르지 않습니다: {path}");
            }
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || prefab == null)
            {
                throw new InvalidOperationException($"프리팹 저장 실패: {path}");
            }
            return prefab;
        }
    }
}
