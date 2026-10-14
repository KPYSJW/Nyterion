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
    /// BoomMaker 애니메이션, 프리팹, 유물 데이터를 구성합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class BoomMakerRelicSetup
    {
        private const string ArtRoot = "Assets/Nytherion/Art/Characters/Companions/RoboPilot";
        private const string SpriteRoot = ArtRoot + "/Sprites";
        private const string AnimationRoot = ArtRoot + "/Animations";
        private const string PrefabRoot = "Assets/Prefabs/Gameplay/Characters/Companions/RoboPilot";
        private const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/BoomMaker.asset";
        private const string RelicIconPath = "Assets/Nytherion/Art/Relics/Sprites/BoomMaker.png";
        private const string DatabasePath = "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset";
        private const string SummonedCreatureSetPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SummonedCreatureSet.asset";
        private const string ExistingExplosionPrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/ExplosionVFX.prefab";

        private const string CompanionIdleTexturePath = SpriteRoot + "/RoboPilotCompanion_Idle.png";
        private const string CompanionWalkTexturePath = SpriteRoot + "/RoboPilotCompanion_Walk.png";
        private const string CompanionSummonTexturePath = SpriteRoot + "/RoboPilotCompanion_Summon.png";
        private const string ExplosiveRobotIdleTexturePath = SpriteRoot + "/ExplosiveRobot_Idle.png";
        private const string ExplosiveRobotRunTexturePath = SpriteRoot + "/ExplosiveRobot_Run.png";

        private const string ExplosiveRobotPrefabPath = PrefabRoot + "/ExplosiveRobot.prefab";
        private const string ExplosionPrefabPath = PrefabRoot + "/RoboPilotExplosionVFX.prefab";
        private const string CompanionPrefabPath = PrefabRoot + "/BoomMaker.prefab";

        static BoomMakerRelicSetup()
        {
            EditorApplication.delayCall += TryAutomaticSetup;
        }

        [MenuItem("Nytherion/Setup BoomMaker Relic")]
        public static void SetupBoomMakerRelic()
        {
            EnsureFolder(AnimationRoot);
            EnsureFolder(PrefabRoot);

            Sprite relicIcon = ConfigureAndLoadRelicIcon();
            // Idle 스프라이트 슬라이스와 클립은 수동 설정을 보존하며 여기서 재생성하지 않습니다.
            ConfigureSpriteSheet(CompanionWalkTexturePath, "RoboPilotCompanion_Walk", 6);
            ConfigureSpriteSheet(CompanionSummonTexturePath, "RoboPilotCompanion_Summon", 6);
            ConfigureSpriteSheet(ExplosiveRobotIdleTexturePath, "ExplosiveRobot_Idle", 1);
            ConfigureSpriteSheet(ExplosiveRobotRunTexturePath, "ExplosiveRobot_Run", 3);

            Sprite[] companionIdleFrames = LoadFrames(CompanionIdleTexturePath);
            Sprite[] companionWalkFrames = LoadFrames(CompanionWalkTexturePath);
            Sprite[] companionSummonFrames = LoadFrames(CompanionSummonTexturePath);
            Sprite[] explosiveRobotIdleFrames = LoadFrames(ExplosiveRobotIdleTexturePath);
            Sprite[] explosiveRobotRunFrames = LoadFrames(ExplosiveRobotRunTexturePath);

            AnimationClip companionIdleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationRoot + "/RoboPilotCompanion_Idle.anim");
            if (companionIdleClip == null)
            {
                throw new MissingReferenceException("수동 설정한 BoomMaker Idle 애니메이션을 찾을 수 없습니다.");
            }
            AnimationClip companionWalkClip = CreateOrUpdateClip(
                AnimationRoot + "/RoboPilotCompanion_Walk.anim",
                string.Empty,
                companionWalkFrames,
                10f,
                true);
            AnimationClip companionSummonClip = CreateOrUpdateClip(
                AnimationRoot + "/RoboPilotCompanion_Summon.anim",
                string.Empty,
                companionSummonFrames,
                12f,
                false,
                "DeployExplosiveRobot",
                2);
            AnimationClip explosiveRobotIdleClip = CreateOrUpdateClip(
                AnimationRoot + "/ExplosiveRobot_Idle.anim",
                string.Empty,
                explosiveRobotIdleFrames,
                1f,
                true);
            AnimationClip explosiveRobotRunClip = CreateOrUpdateClip(
                AnimationRoot + "/ExplosiveRobot_Run.anim",
                string.Empty,
                explosiveRobotRunFrames,
                10f,
                true);

            RuntimeAnimatorController companionController = CreateCompanionController(
                companionIdleClip,
                companionWalkClip,
                companionSummonClip);
            RuntimeAnimatorController explosiveRobotController = CreateExplosiveRobotController(
                explosiveRobotIdleClip,
                explosiveRobotRunClip);

            GameObject explosionPrefab = CreateExplosionPrefab();
            GameObject explosiveRobotPrefab = CreateExplosiveRobotPrefab(
                explosiveRobotIdleFrames[0],
                explosiveRobotController,
                explosionPrefab);
            GameObject companionPrefab = CreateCompanionPrefab(
                companionIdleFrames[0],
                companionController,
                explosiveRobotPrefab);
            RelicData relic = CreateOrUpdateRelic(relicIcon, companionPrefab);
            RegisterRelic(relic);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RelicGachaSync.SyncRelicsToGachaPools(false);
            Debug.Log("[BoomMakerRelicSetup] BoomMaker 아이콘, 애니메이션, 프리팹, 유물 등록을 완료했습니다.", relic);
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

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(CompanionIdleTexturePath) == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(CompanionWalkTexturePath) == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(CompanionSummonTexturePath) == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(ExplosiveRobotIdleTexturePath) == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(ExplosiveRobotRunTexturePath) == null)
            {
                return;
            }

            SetupBoomMakerRelic();
        }

        private static Sprite ConfigureAndLoadRelicIcon()
        {
            TextureImporter importer = AssetImporter.GetAtPath(RelicIconPath) as TextureImporter;
            if (importer == null)
            {
                throw new MissingReferenceException($"BoomMaker 유물 아이콘을 찾을 수 없습니다: {RelicIconPath}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(RelicIconPath);
            if (icon == null)
            {
                throw new MissingReferenceException($"BoomMaker 유물 아이콘을 Sprite로 불러올 수 없습니다: {RelicIconPath}");
            }

            return icon;
        }

        private static void ConfigureSpriteSheet(string path, string spriteName, int frameCount)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new MissingReferenceException($"스프라이트 원본을 찾을 수 없습니다: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            Texture2D sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int textureWidth = sourceTexture.width;
            int textureHeight = sourceTexture.height;
            int frameWidth = textureWidth / Mathf.Max(1, frameCount);

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            ISpriteNameFileIdDataProvider nameFileIdProvider =
                dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            List<SpriteNameFileIdPair> existingNamePairs = nameFileIdProvider
                .GetNameFileIdPairs()
                .ToList();
            SpriteRect[] sprites = new SpriteRect[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                string frameName = $"{spriteName}_{i:00}";
                string frameSuffix = $"_{i:00}";
                SpriteNameFileIdPair existingPair = existingNamePairs.FirstOrDefault(pair =>
                        pair.name.EndsWith(frameSuffix) && pair.name != frameName) ??
                    existingNamePairs.FirstOrDefault(pair => pair.name == frameName);
                GUID spriteId = existingPair != null ? existingPair.GetFileGUID() : GUID.Generate();
                sprites[i] = new SpriteRect
                {
                    name = frameName,
                    rect = new Rect(i * frameWidth, 0f, frameWidth, textureHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = spriteId
                };
            }

            nameFileIdProvider.SetNameFileIdPairs(Enumerable.Empty<SpriteNameFileIdPair>());
            dataProvider.SetSpriteRects(sprites);
            nameFileIdProvider.SetNameFileIdPairs(
                sprites.Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID)));
            dataProvider.Apply();
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
            string spriteRendererPath,
            Sprite[] frames,
            float frameRate,
            bool loop,
            string eventFunction = null,
            int eventFrame = 0)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
            clip.frameRate = frameRate;
            foreach (EditorCurveBinding existingBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                AnimationUtility.SetObjectReferenceCurve(clip, existingBinding, null);
            }

            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = spriteRendererPath,
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
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationEvent[] animationEvents = string.IsNullOrEmpty(eventFunction)
                ? System.Array.Empty<AnimationEvent>()
                : new[]
                {
                    new AnimationEvent
                    {
                        time = Mathf.Max(0, eventFrame) / frameRate,
                        functionName = eventFunction
                    }
                };
            AnimationUtility.SetAnimationEvents(clip, animationEvents);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static RuntimeAnimatorController CreateCompanionController(
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip summonClip)
        {
            string path = AnimationRoot + "/RoboPilotCompanion.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("SummonExplosiveRobot", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState("Idle");
            AnimatorState walkState = stateMachine.AddState("Walk");
            AnimatorState summonState = stateMachine.AddState("Summon Explosive Robot");
            idleState.motion = idleClip;
            walkState.motion = walkClip;
            summonState.motion = summonClip;
            stateMachine.defaultState = idleState;

            AddBoolTransition(idleState, walkState, "IsWalking", true);
            AddBoolTransition(walkState, idleState, "IsWalking", false);

            AnimatorStateTransition summonTransition = stateMachine.AddAnyStateTransition(summonState);
            summonTransition.hasExitTime = false;
            summonTransition.duration = 0f;
            summonTransition.canTransitionToSelf = false;
            summonTransition.AddCondition(AnimatorConditionMode.If, 0f, "SummonExplosiveRobot");

            AnimatorStateTransition returnTransition = summonState.AddTransition(idleState);
            returnTransition.hasExitTime = true;
            returnTransition.exitTime = 0.95f;
            returnTransition.duration = 0.05f;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static RuntimeAnimatorController CreateExplosiveRobotController(AnimationClip idleClip, AnimationClip runClip)
        {
            string path = AnimationRoot + "/ExplosiveRobot.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState("Idle");
            AnimatorState runState = stateMachine.AddState("Run");
            idleState.motion = idleClip;
            runState.motion = runClip;
            stateMachine.defaultState = idleState;
            AddBoolTransition(idleState, runState, "IsRunning", true);
            AddBoolTransition(runState, idleState, "IsRunning", false);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddBoolTransition(
            AnimatorState from,
            AnimatorState to,
            string parameter,
            bool expectedValue)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameter);
        }

        private static GameObject CreateExplosionPrefab()
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExistingExplosionPrefabPath);
            SpriteRenderer sourceRenderer = sourcePrefab != null ? sourcePrefab.GetComponent<SpriteRenderer>() : null;
            Animator sourceAnimator = sourcePrefab != null ? sourcePrefab.GetComponent<Animator>() : null;

            GameObject root = new GameObject("RoboPilotExplosionVFX");
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            if (sourceRenderer != null)
            {
                renderer.sprite = sourceRenderer.sprite;
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
            }
            renderer.sortingOrder = 2;

            Animator animator = root.AddComponent<Animator>();
            if (sourceAnimator != null)
            {
                animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            }

            PooledExplosionVFX pooledVfx = root.AddComponent<PooledExplosionVFX>();
            pooledVfx.poolTag = root.name;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExplosionPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateExplosiveRobotPrefab(
            Sprite idleSprite,
            RuntimeAnimatorController controller,
            GameObject explosionPrefab)
        {
            GameObject root = new GameObject("ExplosiveRobot");
            root.transform.localScale = Vector3.one * 1.5f;

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = idleSprite;
            renderer.sortingOrder = 1;

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.drag = 0f;
            rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.11f;

            ExplosiveRobot robot = root.AddComponent<ExplosiveRobot>();
            SerializedObject serializedRobot = new SerializedObject(robot);
            serializedRobot.FindProperty("moveSpeed").floatValue = 3.25f;
            serializedRobot.FindProperty("contactDetonationRadius").floatValue = 0.25f;
            serializedRobot.FindProperty("targetSearchRange").floatValue = 12f;
            serializedRobot.FindProperty("lifetime").floatValue = 5f;
            serializedRobot.FindProperty("explosionRadius").floatValue = 1.5f;
            serializedRobot.FindProperty("explosionVfxPrefab").objectReferenceValue = explosionPrefab;
            serializedRobot.FindProperty("explosionVfxScale").floatValue = 1.5f;
            serializedRobot.FindProperty("runningBoolParam").stringValue = "IsRunning";
            serializedRobot.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExplosiveRobotPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCompanionPrefab(
            Sprite idleSprite,
            RuntimeAnimatorController controller,
            GameObject explosiveRobotPrefab)
        {
            GameObject root = new GameObject("BoomMaker");
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
            visual.AddComponent<BoomMakerAnimationEventRelay>();

            BoomMaker companion = root.AddComponent<BoomMaker>();
            SerializedObject serializedCompanion = new SerializedObject(companion);
            SetObjectReference(serializedCompanion, "visual", visual);
            SetFloat(serializedCompanion, "followOffsetX", 1f);
            SetFloat(serializedCompanion, "followOffsetY", 0.45f);
            SetFloat(serializedCompanion, "wakeupDistance", 0.8f);
            SetFloat(serializedCompanion, "stopRadius", 0.4f);
            SetFloat(serializedCompanion, "leashRange", 12f);
            SetFloat(serializedCompanion, "minMoveSpeed", 5f);
            SetFloat(serializedCompanion, "maxMoveSpeed", 6.5f);
            SetFloat(serializedCompanion, "acceleration", 18f);
            SetFloat(serializedCompanion, "attackRange", 7f);
            SetFloat(serializedCompanion, "attackInterval", 2.5f);
            SetFloat(serializedCompanion, "attackFreezeDuration", 0.5f);
            SetFloat(serializedCompanion, "baseDamage", 8f);
            SetFloat(serializedCompanion, "weaponDamageRatio", 0.8f);
            SetFloat(serializedCompanion, "damageRatioPerLevel", 0.15f);
            SetString(serializedCompanion, "walkingBoolParam", "IsWalking");
            SetString(serializedCompanion, "jumpTriggerParam", string.Empty);
            SetString(serializedCompanion, "attackTriggerParam", "SummonExplosiveRobot");
            SetString(serializedCompanion, "alternateAttackTriggerParam", string.Empty);
            SetObjectReference(serializedCompanion, "explosiveRobotPrefab", explosiveRobotPrefab);
            serializedCompanion.FindProperty("explosiveRobotSpawnOffset").vector3Value = new Vector3(0f, -0.2f, 0f);
            serializedCompanion.FindProperty("maximumActiveRobots").intValue = 3;
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

            relic.name = "BoomMaker";
            relic.relicName = "BoomMaker";
            relic.koreanName = "BoomMaker";
            relic.description_KR = "BoomMaker를 소환합니다. BoomMaker는 적을 발견하면 회전하며 폭발 로봇을 내보내고, 폭발 로봇은 적에게 달려가 충돌 시 범위 피해를 줍니다.";
            relic.description_EN = "Summons BoomMaker. When it finds an enemy, it spins to deploy an explosive robot that runs into the enemy and deals area damage.";
            relic.Image = icon;
            relic.rarity = Rarity.Epic;
            relic.level = 1;
            relic.isDisabled = false;
            relic.grantsProjectileHoming = false;
            relic.shape = new List<Vector2Int> { Vector2Int.zero };
            relic.synergySeriesId = "SummonedCreatureSeries";
            relic.synergySetBonusData = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(SummonedCreatureSetPath);
            relic.unlockMilestoneID = string.Empty;
            relic.effectModules = new List<RelicEffectModule>
            {
                new RelicEffectModule
                {
                    description_KR = "적을 추적해 폭발하는 폭발 로봇을 주기적으로 소환합니다.",
                    description_EN = "Periodically deploys an explosive robot that chases enemies and explodes.",
                    effects = new List<RelicEffectBase>
                    {
                        new SummonedCompanionRelicEffect
                        {
                            companionPrefab = companionPrefab,
                            spawnOffset = new Vector3(-1f, 0.45f, 0f)
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
                Debug.LogError("[BoomMakerRelicSetup] RelicDatabase 또는 BoomMaker 유물을 찾을 수 없습니다.");
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
