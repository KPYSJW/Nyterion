using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    public static class LaserWeaponSetup
    {
        private const string DATA_PATH = "Assets/Nytherion/Data/ScriptableObjects/Weapons/LaserEmitter.asset";
        private const string WEAPON_PATH = "Assets/Prefabs/Gameplay/Combat/Weapons/LaserEmitter.prefab";
        private const string BEAM_PATH = "Assets/Prefabs/Gameplay/Combat/Proj/Player_WeaponLaser.prefab";
        private const string DATABASE_PATH = "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset";
        private const string REFERENCE_WEAPON_PATH = "Assets/Nytherion/Data/ScriptableObjects/Weapons/LunarSpark.asset";
        private const string LASER_MATERIAL_PATH = "Assets/Nytherion/Art/Common/Materials/LaserEmitter.mat";
        private const string ENDPOINT_SPRITE_PATH = "Assets/Nytherion/Art/Combat/VFX/Sprites/LaserStartEnd.png";
        private const string ENDPOINT_CONTROLLER_PATH = "Assets/Nytherion/Art/Combat/VFX/Animations/LaserEffect/LaserStartEnd/LaserStartEnd_0.controller";

        [MenuItem("Tools/Nytherion/Laser Weapon/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[LaserWeaponSetup] 플레이 모드를 종료한 뒤 레이저 무기를 생성해 주세요.");
                return;
            }

            try
            {
                CreateAndRegisterAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LaserWeaponSetup] 레이저 무기 에셋 생성 실패: {exception.Message}");
                throw;
            }
        }

        private static void CreateAndRegisterAssets()
        {
            // 참조와 기존 파일의 타입을 먼저 검사해 사용자 에셋을 덮어쓰지 않는다.
            WeaponData referenceWeapon = RequireAsset<WeaponData>(REFERENCE_WEAPON_PATH);
            Material laserMaterial = RequireAsset<Material>(LASER_MATERIAL_PATH);
            Sprite endpointSprite = RequireSprite(ENDPOINT_SPRITE_PATH, "LaserStartEnd_0");
            RuntimeAnimatorController endpointController = RequireAsset<RuntimeAnimatorController>(ENDPOINT_CONTROLLER_PATH);
            ItemDatabaseSO database = RequireAsset<ItemDatabaseSO>(DATABASE_PATH);
            SpriteRenderer referenceRenderer = referenceWeapon.weaponPrefab != null
                ? referenceWeapon.weaponPrefab.GetComponentInChildren<SpriteRenderer>() : null;
            if (referenceWeapon.weaponSprite == null || referenceWeapon.icon == null ||
                referenceRenderer == null || referenceRenderer.sharedMaterial == null)
            {
                throw new InvalidOperationException("루나 스파크 무기의 외형 참조가 비어 있습니다.");
            }

            int targetMask = LayerMask.GetMask("Enemy");
            int obstructionMask = LayerMask.GetMask("Wall", "Obstacle");
            if (targetMask == 0 || LayerMask.NameToLayer("Wall") < 0 || LayerMask.NameToLayer("Obstacle") < 0)
            {
                throw new InvalidOperationException("Enemy, Wall, Obstacle 레이어 설정을 확인해 주세요.");
            }

            LaserWeaponData data = LoadExistingAsset<LaserWeaponData>(DATA_PATH);
            GameObject weaponPrefab = LoadExistingAsset<GameObject>(WEAPON_PATH);
            GameObject beamPrefab = LoadExistingAsset<GameObject>(BEAM_PATH);
            if (weaponPrefab != null && weaponPrefab.GetComponent<LaserWeapon>() == null)
            {
                throw new InvalidOperationException($"기존 {WEAPON_PATH}에 LaserWeapon 컴포넌트가 없습니다.");
            }
            if (beamPrefab != null && beamPrefab.GetComponent<WeaponLaserBeam>() == null)
            {
                throw new InvalidOperationException($"기존 {BEAM_PATH}에 WeaponLaserBeam 컴포넌트가 없습니다.");
            }

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<LaserWeaponData>();
                data.itemName_KR = "레이저 이미터";
                data.itemName_EN = "Laser Emitter";
                data.description_KR = "발사 지점에서 일정 시간 레이저를 방출하여 범위 안의 적에게 주기적으로 피해를 줍니다. 발사가 끝나면 재사용 대기시간이 시작됩니다.";
                data.description_EN = "Emits a beam from the muzzle for a fixed duration, dealing periodic damage to enemies in its path. Cooldown begins when firing ends.";
                data.icon = referenceWeapon.icon;
                data.weaponSprite = referenceWeapon.weaponSprite;
                data.weaponType = WeaponType.Ranged;
                data.equipmentType = EquipmentType.Weapon;
                data.baseValue = 150;
                data.damage = 4f;
                data.range = 8f;
                data.cooldown = 0.6f;
                data.fireDuration = 1.2f;
                data.tickInterval = 0.2f;
                data.damageTickCount = 3;
                data.beamWidth = 0.55f;
                data.visualBeamWidth = 1f;
                data.fadeDuration = 0.12f;
                data.followAim = true;
                data.targetLayers = targetMask;
                data.obstructionLayers = obstructionMask;
                data.visualSegments = 18;
                data.jitterMagnitude = 0f;
                data.jitterInterval = 0.035f;
                data.textureTileLength = 1f;
                data.firePointOffset = referenceWeapon.firePointOffset;
                data.visualPositionOffset = referenceWeapon.visualPositionOffset;
                data.spriteRotationOffset = referenceWeapon.spriteRotationOffset;
                data.visualScale = 1f;
                data.useStaffRecoil = false;
                data.isArchivable = false;
                data.projectileSpeed = 0f;
                data.isStackable = false;
                data.maxStack = 1;
                AssetDatabase.CreateAsset(data, DATA_PATH);
            }

            if (beamPrefab == null)
            {
                beamPrefab = CreateBeamPrefab(laserMaterial, endpointSprite, endpointController);
            }
            else
            {
                RepairBeamReferences(laserMaterial, endpointSprite, endpointController);
            }

            if (weaponPrefab == null)
            {
                weaponPrefab = CreateWeaponPrefab(data, referenceRenderer.sharedMaterial);
            }
            else
            {
                RepairWeaponReferences(data);
            }

            bool dataChanged = false;
            if (data.weaponPrefab == null)
            {
                data.weaponPrefab = weaponPrefab.GetComponent<LaserWeapon>();
                dataChanged = true;
            }
            if (data.projectilePrefab == null)
            {
                data.projectilePrefab = beamPrefab;
                dataChanged = true;
            }
            if (string.IsNullOrEmpty(data.ID))
            {
                SerializedObject serializedData = new SerializedObject(data);
                serializedData.FindProperty("uniqueID").stringValue = Guid.NewGuid().ToString();
                serializedData.ApplyModifiedPropertiesWithoutUndo();
                dataChanged = true;
            }
            if (dataChanged) EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            if (database.allItems == null) database.allItems = new List<ItemData>();
            if (!database.allItems.Contains(data))
            {
                Undo.RecordObject(database, "레이저 무기 데이터베이스 등록");
                database.allItems.Add(data);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssetIfDirty(database);
            }

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            Debug.Log($"[LaserWeaponSetup] 레이저 무기와 아이템 데이터베이스 연결 완료: {DATA_PATH}", data);
        }

        private static GameObject CreateBeamPrefab(Material laserMaterial, Sprite endpointSprite,
            RuntimeAnimatorController endpointController)
        {
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("Player_WeaponLaser");
                SceneManager.MoveGameObjectToScene(root, previewScene);
                WeaponLaserBeam beam = root.AddComponent<WeaponLaserBeam>();
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                LineRenderer renderer = visual.AddComponent<LineRenderer>();
                renderer.sharedMaterial = laserMaterial;
                renderer.useWorldSpace = true;
                renderer.positionCount = 2;
                renderer.SetPosition(0, Vector3.zero);
                renderer.SetPosition(1, Vector3.right);
                renderer.numCornerVertices = 2;
                renderer.numCapVertices = 0;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = 2;

                bool unused = false;
                SpriteRenderer startEffect = EnsureEndpointEffect(root.transform, "StartEffect",
                    endpointSprite, endpointController, 0f, ref unused);
                SpriteRenderer endEffect = EnsureEndpointEffect(root.transform, "EndEffect",
                    endpointSprite, endpointController, 180f, ref unused);

                SerializedObject serializedBeam = new SerializedObject(beam);
                serializedBeam.FindProperty("lineRenderer").objectReferenceValue = renderer;
                serializedBeam.FindProperty("startEffectRenderer").objectReferenceValue = startEffect;
                serializedBeam.FindProperty("endEffectRenderer").objectReferenceValue = endEffect;
                serializedBeam.ApplyModifiedPropertiesWithoutUndo();
                return SaveNewPrefab(root, BEAM_PATH);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static GameObject CreateWeaponPrefab(LaserWeaponData data, Material material)
        {
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("LaserEmitter");
                SceneManager.MoveGameObjectToScene(root, previewScene);
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = data.weaponSprite;
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 1;
                GameObject muzzle = new GameObject("FirePoint");
                muzzle.transform.SetParent(root.transform, false);
                muzzle.transform.localPosition = data.firePointOffset;
                LaserWeapon weapon = root.AddComponent<LaserWeapon>();
                weapon.weaponData = data;
                weapon.firePoint = muzzle.transform;
                weapon.projectilePoolTag = "Player_WeaponLaser";
                return SaveNewPrefab(root, WEAPON_PATH);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void RepairBeamReferences(Material laserMaterial, Sprite endpointSprite,
            RuntimeAnimatorController endpointController)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BEAM_PATH);
            try
            {
                WeaponLaserBeam beam = root.GetComponent<WeaponLaserBeam>();
                LineRenderer renderer = root.GetComponentInChildren<LineRenderer>(true);
                bool changed = false;
                if (renderer == null)
                {
                    Transform visual = root.transform.Find("Visual");
                    if (visual == null)
                    {
                        visual = new GameObject("Visual").transform;
                        visual.SetParent(root.transform, false);
                    }
                    renderer = visual.gameObject.AddComponent<LineRenderer>();
                    renderer.useWorldSpace = true;
                    renderer.positionCount = 2;
                    renderer.SetPosition(0, Vector3.zero);
                    renderer.SetPosition(1, Vector3.right);
                    renderer.numCornerVertices = 2;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    changed = true;
                }
                if (renderer.sharedMaterial == null)
                {
                    renderer.sharedMaterial = laserMaterial;
                    changed = true;
                }
                SpriteRenderer startEffect = EnsureEndpointEffect(root.transform, "StartEffect",
                    endpointSprite, endpointController, 0f, ref changed);
                SpriteRenderer endEffect = EnsureEndpointEffect(root.transform, "EndEffect",
                    endpointSprite, endpointController, 180f, ref changed);
                SerializedObject serializedBeam = new SerializedObject(beam);
                changed |= AssignMissingReference(serializedBeam, "lineRenderer", renderer);
                changed |= AssignMissingReference(serializedBeam, "startEffectRenderer", startEffect);
                changed |= AssignMissingReference(serializedBeam, "endEffectRenderer", endEffect);
                if (changed)
                {
                    serializedBeam.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, BEAM_PATH);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RepairWeaponReferences(LaserWeaponData data)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(WEAPON_PATH);
            try
            {
                LaserWeapon weapon = root.GetComponent<LaserWeapon>();
                bool changed = false;
                if (weapon.weaponData == null)
                {
                    weapon.weaponData = data;
                    changed = true;
                }
                if (weapon.firePoint == null && root.transform.Find("FirePoint") != null)
                {
                    weapon.firePoint = root.transform.Find("FirePoint");
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, WEAPON_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool AssignMissingReference(SerializedObject target, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null || value == null) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private static SpriteRenderer EnsureEndpointEffect(Transform root, string name, Sprite sprite,
            RuntimeAnimatorController controller, float localAngle, ref bool changed)
        {
            Transform endpoint = root.Find(name);
            if (endpoint == null)
            {
                endpoint = new GameObject(name).transform;
                endpoint.SetParent(root, false);
                changed = true;
            }

            Quaternion expectedRotation = Quaternion.Euler(0f, 0f, localAngle);
            if (Quaternion.Angle(endpoint.localRotation, expectedRotation) > 0.01f)
            {
                endpoint.localRotation = expectedRotation;
                changed = true;
            }

            SpriteRenderer renderer = endpoint.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = endpoint.gameObject.AddComponent<SpriteRenderer>();
                changed = true;
            }
            if (renderer.sprite != sprite)
            {
                renderer.sprite = sprite;
                changed = true;
            }
            if (renderer.sortingOrder != 3)
            {
                renderer.sortingOrder = 3;
                changed = true;
            }

            Animator animator = endpoint.GetComponent<Animator>();
            if (animator == null)
            {
                animator = endpoint.gameObject.AddComponent<Animator>();
                changed = true;
            }
            if (animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                changed = true;
            }
            return renderer;
        }

        private static GameObject SaveNewPrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || saved == null) throw new InvalidOperationException($"프리팹 저장에 실패했습니다: {path}");
            return saved;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = LoadExistingAsset<T>(path);
            if (asset == null) throw new InvalidOperationException($"필수 참조 에셋을 찾을 수 없습니다: {path}");
            return asset;
        }

        private static Sprite RequireSprite(string path, string spriteName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.name == spriteName) return sprite;
            }
            throw new InvalidOperationException($"필수 스프라이트를 찾을 수 없습니다: {path}/{spriteName}");
        }

        private static T LoadExistingAsset<T>(string path) where T : UnityEngine.Object
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null)
            {
                if (System.IO.File.Exists(path))
                {
                    throw new InvalidOperationException($"기존 파일을 Unity 에셋으로 읽을 수 없습니다: {path}");
                }
                return null;
            }
            if (!(existing is T asset)) throw new InvalidOperationException($"기존 에셋의 타입이 예상과 다릅니다: {path}");
            return asset;
        }
    }
}
