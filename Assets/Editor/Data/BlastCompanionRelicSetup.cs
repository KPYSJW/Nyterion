using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Companions;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>
    /// 블래스트 소환수의 스프라이트, 애니메이션, 프리팹과 유물 데이터를 구성합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class BlastCompanionRelicSetup
    {
        private const string ArtRoot = "Assets/Nytherion/Art/Characters/Companions/ComradeBlast";
        private const string SpriteRoot = ArtRoot + "/Sprites";
        private const string AnimationRoot = ArtRoot + "/Animations";
        private const string PrefabRoot = "Assets/Prefabs/Gameplay/Characters/Companions/ComradeBlast";
        private const string ProjectilePrefabRoot = "Assets/Prefabs/Gameplay/Combat/Proj";
        private const string RelicPath =
            "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/ComradeBlast.asset";
        private const string DatabasePath =
            "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset";
        private const string SummonedCreatureSetPath =
            "Assets/Nytherion/Data/ScriptableObjects/Relics/SummonedCreatureSet.asset";

        private const string IdleTexturePath = SpriteRoot + "/Blast_Idle.png";
        private const string WalkTexturePath = SpriteRoot + "/Blast_Movement.png";
        private const string MissileTexturePath = SpriteRoot + "/Missile.png";
        private const string CompanionPrefabPath = PrefabRoot + "/ComradeBlast.prefab";
        private const string MissilePrefabPath = ProjectilePrefabRoot + "/ComradeBlastMissile.prefab";

        static BlastCompanionRelicSetup()
        {
            EditorApplication.delayCall += TryAutomaticSetup;
        }

        [MenuItem("Nytherion/Setup Comrade Blast Relic")]
        public static void SetupComradeBlastRelic()
        {
            EnsureFolder(AnimationRoot);
            EnsureFolder(PrefabRoot);

            ConfigureSpriteSheet(IdleTexturePath, "Blast_Idle", 3);
            ConfigureSpriteSheet(WalkTexturePath, "Blast_Walk", 2);
            Sprite missileSprite = ConfigureSingleSprite(MissileTexturePath);

            Sprite[] idleFrames = LoadFrames(IdleTexturePath);
            Sprite[] walkFrames = LoadFrames(WalkTexturePath);
            AnimationClip idleClip = CreateOrUpdateClip(
                AnimationRoot + "/ComradeBlast_Idle.anim",
                idleFrames,
                8f);
            AnimationClip walkClip = CreateOrUpdateClip(
                AnimationRoot + "/ComradeBlast_Walk.anim",
                walkFrames,
                8f);
            RuntimeAnimatorController controller = CreateOrUpdateController(idleClip, walkClip);

            GameObject missilePrefab = CreateMissilePrefab(missileSprite);
            GameObject companionPrefab = CreateCompanionPrefab(idleFrames[0], controller, missilePrefab);
            RelicData relic = CreateOrUpdateRelic(idleFrames[0], companionPrefab);
            RegisterRelic(relic);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RelicGachaSync.SyncRelicsToGachaPools(false);
            Debug.Log("[BlastCompanionRelicSetup] 블래스트 소환수 유물 구성을 완료했습니다.", relic);
        }

        private static void TryAutomaticSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutomaticSetup;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath) != null)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(IdleTexturePath) == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(WalkTexturePath) == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(MissileTexturePath) == null)
            {
                return;
            }

            SetupComradeBlastRelic();
        }

        private static void ConfigureSpriteSheet(string path, string spriteName, int frameCount)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new MissingReferenceException($"스프라이트 원본을 찾을 수 없습니다: {path}");
            }

            ConfigureTexture(importer, SpriteImportMode.Multiple);
            Texture2D sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int frameWidth = sourceTexture.width / Mathf.Max(1, frameCount);

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            ISpriteNameFileIdDataProvider nameFileIdProvider =
                dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            List<SpriteNameFileIdPair> existingNamePairs = nameFileIdProvider
                .GetNameFileIdPairs()
                .ToList();
            SpriteRect[] spriteRects = new SpriteRect[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                string frameName = $"{spriteName}_{i:00}";
                SpriteNameFileIdPair existingPair =
                    existingNamePairs.FirstOrDefault(pair => pair.name == frameName);
                spriteRects[i] = new SpriteRect
                {
                    name = frameName,
                    rect = new Rect(i * frameWidth, 0f, frameWidth, sourceTexture.height),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = existingPair != null ? existingPair.GetFileGUID() : GUID.Generate()
                };
            }

            nameFileIdProvider.SetNameFileIdPairs(Enumerable.Empty<SpriteNameFileIdPair>());
            dataProvider.SetSpriteRects(spriteRects);
            nameFileIdProvider.SetNameFileIdPairs(
                spriteRects.Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID)));
            dataProvider.Apply();
            importer.SaveAndReimport();
        }

        private static Sprite ConfigureSingleSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new MissingReferenceException($"스프라이트 원본을 찾을 수 없습니다: {path}");
            }

            ConfigureTexture(importer, SpriteImportMode.Single);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new MissingReferenceException($"Sprite를 불러올 수 없습니다: {path}");
            }

            return sprite;
        }

        private static void ConfigureTexture(TextureImporter importer, SpriteImportMode importMode)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = importMode;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static Sprite[] LoadFrames(string path)
        {
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name)
                .ToArray();
            if (frames.Length == 0)
            {
                throw new MissingReferenceException($"슬라이스된 스프라이트가 없습니다: {path}");
            }

            return frames;
        }

        private static AnimationClip CreateOrUpdateClip(
            string path,
            Sprite[] frames,
            float frameRate)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
            clip.frameRate = frameRate;
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };
            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / frameRate,
                    value = frames[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static RuntimeAnimatorController CreateOrUpdateController(
            AnimationClip idleClip,
            AnimationClip walkClip)
        {
            string path = AnimationRoot + "/ComradeBlast.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState("Idle");
            AnimatorState walkState = stateMachine.AddState("Walk");
            idleState.motion = idleClip;
            walkState.motion = walkClip;
            stateMachine.defaultState = idleState;
            AddBoolTransition(idleState, walkState, true);
            AddBoolTransition(walkState, idleState, false);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddBoolTransition(
            AnimatorState from,
            AnimatorState to,
            bool expectedValue)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                "IsWalking");
        }

        private static GameObject CreateMissilePrefab(Sprite missileSprite)
        {
            GameObject root = new GameObject("ComradeBlastMissile");
            root.tag = "Weapon";

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = missileSprite;
            renderer.sortingOrder = 1;

            Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            rigidbody.gravityScale = 0f;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
            collider.isTrigger = true;
            collider.direction = CapsuleDirection2D.Horizontal;
            collider.size = new Vector2(0.78f, 0.24f);

            CollisionObject collisionObject = root.AddComponent<CollisionObject>();
            collisionObject.poolTag = root.name;
            root.AddComponent<ProjDistanceLimit>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MissilePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCompanionPrefab(
            Sprite idleSprite,
            RuntimeAnimatorController controller,
            GameObject missilePrefab)
        {
            GameObject root = new GameObject("ComradeBlast");
            Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.drag = 3.5f;
            rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = idleSprite;
            Animator animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            BlastCompanion companion = root.AddComponent<BlastCompanion>();
            SerializedObject serializedCompanion = new SerializedObject(companion);
            SetObjectReference(serializedCompanion, "visual", visual);
            serializedCompanion.FindProperty("invertFacing").boolValue = true;
            SetFloat(serializedCompanion, "followOffsetX", 1.15f);
            SetFloat(serializedCompanion, "followOffsetY", 0.55f);
            SetFloat(serializedCompanion, "wakeupDistance", 0.75f);
            SetFloat(serializedCompanion, "stopRadius", 0.35f);
            SetFloat(serializedCompanion, "leashRange", 12f);
            SetFloat(serializedCompanion, "minMoveSpeed", 3.5f);
            SetFloat(serializedCompanion, "maxMoveSpeed", 5f);
            SetFloat(serializedCompanion, "acceleration", 16f);
            SetFloat(serializedCompanion, "attackRange", 7.5f);
            SetFloat(serializedCompanion, "attackInterval", 1.25f);
            SetFloat(serializedCompanion, "attackFreezeDuration", 0.1f);
            SetObjectReference(serializedCompanion, "projectilePrefab", missilePrefab);
            SetString(serializedCompanion, "projectilePoolTag", string.Empty);
            serializedCompanion.FindProperty("projectileSpawnOffset").vector3Value =
                new Vector3(-0.48f, 0.05f, 0f);
            SetFloat(serializedCompanion, "projectileSpeed", 8f);
            SetFloat(serializedCompanion, "projectileRotationOffset", 180f);
            SetFloat(serializedCompanion, "projectileTravelDistance", 9f);
            SetFloat(serializedCompanion, "baseDamage", 6f);
            SetFloat(serializedCompanion, "weaponDamageRatio", 0.45f);
            SetFloat(serializedCompanion, "damageRatioPerLevel", 0.1f);
            SetString(serializedCompanion, "walkingBoolParam", "IsWalking");
            SetString(serializedCompanion, "jumpTriggerParam", string.Empty);
            SetString(serializedCompanion, "attackTriggerParam", string.Empty);
            SetString(serializedCompanion, "alternateAttackTriggerParam", string.Empty);
            serializedCompanion.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CompanionPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static RelicData CreateOrUpdateRelic(Sprite icon, GameObject companionPrefab)
        {
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                AssetDatabase.CreateAsset(relic, RelicPath);
            }

            relic.name = "ComradeBlast";
            relic.relicName = "Comrade Blast";
            relic.koreanName = "블래스트";
            relic.description_KR =
                "플레이어를 따라다니며 가장 가까운 적에게 미사일을 발사하는 블래스트 소환수를 부릅니다.\n" +
                "유물을 장착하는 동안 유지됩니다.";
            relic.description_EN =
                "Summons Blast, a companion that follows the player and fires missiles at the nearest enemy. " +
                "It remains while equipped.";
            relic.Image = icon;
            relic.rarity = Rarity.Epic;
            relic.level = 1;
            relic.isDisabled = false;
            relic.grantsProjectileHoming = false;
            relic.shape = new List<Vector2Int> { Vector2Int.zero };
            relic.synergySeriesId = "SummonedCreatureSeries";
            relic.synergySetBonusData =
                AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(SummonedCreatureSetPath);
            relic.unlockMilestoneID = string.Empty;
            relic.effectModules = new List<RelicEffectModule>
            {
                new RelicEffectModule
                {
                    description_KR = "플레이어를 따라다니며 가까운 적에게 미사일을 발사합니다.",
                    description_EN = "Follows the player and fires missiles at nearby enemies.",
                    condition = new AlwaysTrueCondition(),
                    effects = new List<RelicEffectBase>
                    {
                        new SummonedCompanionRelicEffect
                        {
                            companionPrefab = companionPrefab,
                            spawnOffset = new Vector3(-1.15f, 0.55f, 0f)
                        }
                    }
                }
            };
            RelicInfluencePolicy.Normalize(relic);
            EditorUtility.SetDirty(relic);
            return relic;
        }

        private static void RegisterRelic(RelicData relic)
        {
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(DatabasePath);
            if (database == null || relic == null)
            {
                Debug.LogError("[BlastCompanionRelicSetup] RelicDatabase 또는 블래스트 유물을 찾을 수 없습니다.");
                return;
            }

            if (database.allRelics == null)
            {
                database.allRelics = new List<RelicData>();
            }
            if (!database.allRelics.Contains(relic))
            {
                database.allRelics.Add(relic);
                database.allRelics = database.allRelics
                    .Where(item => item != null)
                    .Distinct()
                    .OrderBy(item => item.relicName)
                    .ToList();
                EditorUtility.SetDirty(database);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            serializedObject.FindProperty(propertyName).floatValue = value;
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            serializedObject.FindProperty(propertyName).stringValue = value;
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            Object value)
        {
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        }
    }
}
