using System;
using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    public static class PyrofallWeaponSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/Pyrofall.asset";
        public const string WeaponPath = "Assets/Prefabs/Gameplay/Combat/Weapons/Generated/Pyrofall.prefab";
        public const string ProjectilePath = "Assets/Prefabs/Gameplay/Combat/Proj/PyrofallFireball.prefab";
        public const string HitEffectPath = "Assets/Prefabs/Gameplay/Combat/VFX/PyrofallHitVFX.prefab";

        private const string WeaponSpritePath = "Assets/Nytherion/Art/Combat/Weapons/Sprites/Pyrofall.png";
        private const string IconSpritePath = "Assets/Nytherion/Art/Combat/Weapons/Sprites/Pyrofall_Icon.png";
        private const string FireballSpritePath = "Assets/Nytherion/Art/Combat/VFX/Sprites/FireBall.png";
        private const string ExplosionSpritePath = "Assets/Nytherion/Art/Combat/VFX/Sprites/FireBallExplosion.png";
        private const string CastFlashSpritePath = "Assets/Nytherion/Art/Combat/VFX/Sprites/PyroFallFlash.png";
        private const string HitEffectSpritePath = "Assets/Nytherion/Art/Combat/VFX/Sprites/PyroFallHit.png";
        private const string DatabasePath = "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset";
        private const string RarePoolPath = "Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Weapon/Rare_Weapon.asset";

        [MenuItem("Tools/Nytherion/Pyrofall/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("파이로폴 에셋은 편집 모드에서 생성해 주세요.");
            }

            Sprite weaponSprite = ConfigureSingleSprite(WeaponSpritePath);
            Sprite iconSprite = ConfigureSingleSprite(IconSpritePath);
            Sprite[] fireballFrames = LoadFrames(FireballSpritePath, "FireBall", 8);
            Sprite[] explosionFrames = LoadFrames(ExplosionSpritePath, "FireBallExplosion", 8);
            Sprite[] castFlashFrames = LoadFrames(CastFlashSpritePath, "PyroFallFlash", 7);
            Sprite[] hitEffectFrames = LoadFrames(HitEffectSpritePath, "PyroFallHit", 5);
            ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DatabasePath);
            GachaPoolSO rarePool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(RarePoolPath);
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            if (weaponSprite == null || iconSprite == null ||
                fireballFrames.Length != 8 || explosionFrames.Length != 8 || castFlashFrames.Length != 7 ||
                hitEffectFrames.Length != 5 ||
                database == null || rarePool == null || material == null ||
                LayerMask.NameToLayer("Enemy") < 0)
            {
                throw new InvalidOperationException("파이로폴 이미지, 데이터베이스, 뽑기 풀 또는 Enemy 레이어를 확인해 주세요.");
            }

            WeaponData data = LoadExisting<WeaponData>(DataPath);
            bool isNewData = data == null;
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WeaponData>();
                SerializedObject newData = new SerializedObject(data);
                newData.FindProperty("uniqueID").stringValue = Guid.NewGuid().ToString();
                newData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            GameObject projectilePrefab = CreateOrUpdateProjectilePrefab(
                fireballFrames,
                explosionFrames,
                material);
            GameObject hitEffectPrefab = CreateOrUpdateHitEffectPrefab(hitEffectFrames, material);
            ConfigureData(data, weaponSprite, iconSprite, projectilePrefab, hitEffectPrefab, isNewData);
            GameObject weaponPrefab = CreateOrUpdateWeaponPrefab(data, weaponSprite, castFlashFrames, material);

            data.weaponPrefab = weaponPrefab.GetComponent<PyrofallWeapon>();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            RegisterInDatabase(database, data);
            RegisterInRarePool(rarePool, data);
            AssetDatabase.SaveAssets();
            ValidateAssets(data, weaponPrefab, projectilePrefab, hitEffectPrefab);

            Selection.activeObject = data;
            Debug.Log("[PyrofallWeaponSetup] 파이로폴 데이터, 무기/화염구 프리팹, 폭발 이미지 및 뽑기 풀 연결 완료.", data);
        }

        private static void ConfigureData(
            WeaponData data,
            Sprite weaponSprite,
            Sprite iconSprite,
            GameObject projectilePrefab,
            GameObject hitEffectPrefab,
            bool applyDefaults)
        {
            if (applyDefaults)
            {
                data.itemName_KR = "파이로폴";
                data.itemName_EN = "Pyrofall";
                data.description_KR = "지정한 지점의 카메라 상단에서 화염구를 떨어뜨려 타원형 범위에 폭발 피해를 줍니다.";
                data.description_EN = "Drops a fireball from the top of the camera, damaging enemies in an elliptical blast.";
                data.isStackable = false;
                data.maxStack = 1;
                data.baseValue = 220;
                data.traits = new List<EquipmentTrait> { EquipmentTrait.Fire };
                data.statModifiers = new List<Nytherion.Core.Data.StatModifier>();
                data.damage = 20f;
                data.range = 2.25f;
                data.cooldown = 1.4f;
                data.firePointOffset = Vector3.zero;
                data.spriteRotationOffset = -90f;
                data.visualPositionOffset = new Vector3(-0.4f, -0.1f, 0f);
                data.visualScale = 1f;
                data.projectileSpeed = 12f;
                data.projectileRotationOffset = 0f;
                data.hasHomingProjectiles = false;
            }

            data.equipmentType = EquipmentType.Weapon;
            data.weaponType = WeaponType.Ranged;
            data.weaponSprite = weaponSprite;
            data.icon = iconSprite;
            data.useStaffRecoil = false;
            data.projectilePrefab = projectilePrefab;
            data.hitEffectPrefab = hitEffectPrefab;
            data.isArchivable = false;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        private static GameObject CreateOrUpdateProjectilePrefab(
            Sprite[] fireballFrames,
            Sprite[] explosionFrames,
            Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) == null)
            {
                Scene scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject("PyrofallFireball");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    ConfigureProjectileObject(root, fireballFrames, explosionFrames, material);
                    return SavePrefab(root, ProjectilePath);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(ProjectilePath);
            try
            {
                ConfigureProjectileObject(contents, fireballFrames, explosionFrames, material);
                return PrefabUtility.SaveAsPrefabAsset(contents, ProjectilePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureProjectileObject(
            GameObject root,
            Sprite[] fireballFrames,
            Sprite[] explosionFrames,
            Material material)
        {
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = root.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = fireballFrames[0];
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 15;

            PyrofallProjectile projectile = root.GetComponent<PyrofallProjectile>();
            if (projectile == null)
            {
                projectile = root.AddComponent<PyrofallProjectile>();
            }

            SerializedObject serialized = new SerializedObject(projectile);
            serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            SetSpriteArray(serialized.FindProperty("fireballFrames"), fireballFrames);
            SetSpriteArray(serialized.FindProperty("explosionFrames"), explosionFrames);
            serialized.FindProperty("fallAnimationFps").floatValue = 12f;
            serialized.FindProperty("fallingLoopFrameCount").intValue = 6;
            serialized.FindProperty("explosionAnimationFps").floatValue = 12f;
            serialized.FindProperty("sortingOrder").intValue = 15;
            serialized.FindProperty("impactYOffset").floatValue = 0f;
            serialized.FindProperty("enemyLayers").intValue = LayerMask.GetMask("Enemy");
            serialized.FindProperty("overlapBufferSize").intValue = 64;
            serialized.FindProperty("explosionContentWidthFraction").floatValue = 60f / 64f;
            serialized.FindProperty("explosionEllipseAspect").floatValue = 31f / 60f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateOrUpdateWeaponPrefab(
            WeaponData data,
            Sprite weaponSprite,
            Sprite[] castFlashFrames,
            Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPath) == null)
            {
                Scene scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject("Pyrofall");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    ConfigureWeaponObject(root, data, weaponSprite, castFlashFrames, material);
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
                ConfigureWeaponObject(contents, data, weaponSprite, castFlashFrames, material);
                return PrefabUtility.SaveAsPrefabAsset(contents, WeaponPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static GameObject CreateOrUpdateHitEffectPrefab(
            Sprite[] hitEffectFrames,
            Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HitEffectPath) == null)
            {
                Scene scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject("PyrofallHitVFX");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    ConfigureHitEffectObject(root, hitEffectFrames, material);
                    return SavePrefab(root, HitEffectPath);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(HitEffectPath);
            try
            {
                ConfigureHitEffectObject(contents, hitEffectFrames, material);
                return PrefabUtility.SaveAsPrefabAsset(contents, HitEffectPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureHitEffectObject(
            GameObject root,
            Sprite[] hitEffectFrames,
            Material material)
        {
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = root.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = hitEffectFrames[0];
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 20;

            PyrofallHitEffect hitEffect = root.GetComponent<PyrofallHitEffect>();
            if (hitEffect == null)
            {
                hitEffect = root.AddComponent<PyrofallHitEffect>();
            }

            SerializedObject serialized = new SerializedObject(hitEffect);
            serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            SetSpriteArray(serialized.FindProperty("frames"), hitEffectFrames);
            serialized.FindProperty("animationFps").floatValue = 12f;
            serialized.FindProperty("sortingOrder").intValue = 20;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureWeaponObject(
            GameObject root,
            WeaponData data,
            Sprite weaponSprite,
            Sprite[] castFlashFrames,
            Material material)
        {
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
            }
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = weaponSprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 1;

            Transform castFlash = visual.Find("CastFlash");
            if (castFlash == null)
            {
                castFlash = new GameObject("CastFlash").transform;
                castFlash.SetParent(visual, false);
            }
            castFlash.localPosition = new Vector3(0f, 0.68f, 0f);
            castFlash.localRotation = Quaternion.identity;
            castFlash.localScale = Vector3.one * 0.75f;

            SpriteRenderer castFlashRenderer = castFlash.GetComponent<SpriteRenderer>();
            if (castFlashRenderer == null)
            {
                castFlashRenderer = castFlash.gameObject.AddComponent<SpriteRenderer>();
            }
            castFlashRenderer.sprite = castFlashFrames[0];
            castFlashRenderer.sharedMaterial = material;
            castFlashRenderer.sortingOrder = 2;
            castFlashRenderer.enabled = false;

            SpriteRenderer legacyRootRenderer = root.GetComponent<SpriteRenderer>();
            if (legacyRootRenderer != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyRootRenderer);
            }

            PyrofallWeapon weapon = root.GetComponent<PyrofallWeapon>();
            if (weapon == null)
            {
                weapon = root.AddComponent<PyrofallWeapon>();
            }
            weapon.weaponData = data;

            SerializedObject serialized = new SerializedObject(weapon);
            serialized.FindProperty("cameraTopPadding").floatValue = 0f;
            serialized.FindProperty("minimumDropHeight").floatValue = 1f;
            serialized.FindProperty("autoTargetEnemyLayers").intValue = LayerMask.GetMask("Enemy");
            serialized.FindProperty("autoTargetBufferSize").intValue = 64;
            serialized.FindProperty("castFlashRenderer").objectReferenceValue = castFlashRenderer;
            SetSpriteArray(serialized.FindProperty("castFlashFrames"), castFlashFrames);
            serialized.FindProperty("castFlashAnimationFps").floatValue = 14f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static Sprite[] LoadFrames(string path, string prefix, int frameCount)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return Enumerable.Range(0, frameCount)
                .Select(index => AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .SingleOrDefault(sprite => sprite.name == $"{prefix}_{index}"))
                .Where(sprite => sprite != null)
                .ToArray();
        }

        private static void SetSpriteArray(SerializedProperty property, Sprite[] sprites)
        {
            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
        }

        private static void RegisterInDatabase(ItemDatabaseSO database, WeaponData data)
        {
            if (database.allItems == null)
            {
                database.allItems = new List<ItemData>();
            }
            if (!database.allItems.Contains(data))
            {
                database.allItems.Add(data);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssetIfDirty(database);
            }
        }

        private static void RegisterInRarePool(GachaPoolSO rarePool, WeaponData data)
        {
            if (rarePool.items == null)
            {
                rarePool.items = new List<GachaItemRate>();
            }
            if (!rarePool.items.Any(entry => entry.item == data))
            {
                rarePool.items.Add(new GachaItemRate { item = data, weight = 100 });
                EditorUtility.SetDirty(rarePool);
                AssetDatabase.SaveAssetIfDirty(rarePool);
            }
        }

        private static void ValidateAssets(
            WeaponData data,
            GameObject weaponPrefab,
            GameObject projectilePrefab,
            GameObject hitEffectPrefab)
        {
            if (data == null || weaponPrefab == null || projectilePrefab == null || hitEffectPrefab == null ||
                data.weaponPrefab == null || data.projectilePrefab == null || data.hitEffectPrefab == null ||
                weaponPrefab.GetComponent<PyrofallWeapon>() == null ||
                weaponPrefab.transform.Find("Visual") == null ||
                weaponPrefab.transform.Find("Visual/CastFlash") == null ||
                projectilePrefab.GetComponent<PyrofallProjectile>() == null ||
                hitEffectPrefab.GetComponent<PyrofallHitEffect>() == null)
            {
                throw new InvalidOperationException("파이로폴 생성 결과 검증에 실패했습니다.");
            }
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
