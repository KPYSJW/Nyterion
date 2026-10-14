using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Companions;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>
    /// Opapa 원본 이미지가 준비되면 애니메이션, 프리팹과 소환수 유물을 구성합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class OpapaRelicSetup
    {
        private const string ArtRoot = "Assets/Nytherion/Art/Characters/Companions/Opapa";
        internal const string SpriteRoot = ArtRoot + "/Sprites";
        private const string AnimationRoot = ArtRoot + "/Animations";
        private const string PrefabRoot = "Assets/Prefabs/Gameplay/Characters/Companions/Opapa";
        private const string CompanionPrefabPath = PrefabRoot + "/Opapa.prefab";
        private const string IconPath = "Assets/Nytherion/Art/Relics/Sprites/Opapa.png";
        private const string RelicPath =
            "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Opapa.asset";
        private const string DatabasePath =
            "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset";
        private const string SummonedCreatureSetPath =
            "Assets/Nytherion/Data/ScriptableObjects/Relics/SummonedCreatureSet.asset";

        private const float FrameRate = 10f;

        private sealed class SourceTextures
        {
            public string idle;
            public string walk;
            public string blink;
            public string rollStart;
            public string rolling;
            public string rollStop;
        }

        static OpapaRelicSetup()
        {
            EditorApplication.delayCall += TryAutomaticSetup;
        }

        [MenuItem("Nytherion/Setup Opapa Relic")]
        public static void SetupOpapaRelic()
        {
            if (!TryResolveSourceTextures(out SourceTextures sources))
            {
                Debug.LogError(
                    "[OpapaRelicSetup] Opapa 이미지 6종을 찾을 수 없습니다. " +
                    $"{SpriteRoot}에 Idle, Walk, Blink, Roll_Start, Rolling, Roll_Stop PNG를 추가하세요.");
                return;
            }

            EnsureFolder(AnimationRoot);
            EnsureFolder(PrefabRoot);

            AnimationClip idleClip = CreateClip(sources.idle, "Opapa_Idle", true, null);
            AnimationClip walkClip = CreateClip(sources.walk, "Opapa_Walk", true, null);
            AnimationClip blinkClip = CreateClip(sources.blink, "Opapa_Blink", false, null);
            AnimationClip rollStartClip = CreateClip(
                sources.rollStart,
                "Opapa_Roll_Start",
                false,
                nameof(OpapaCompanion.BeginRolling));
            AnimationClip rollingClip = CreateClip(sources.rolling, "Opapa_Rolling", true, null);
            AnimationClip rollStopClip = CreateClip(
                sources.rollStop,
                "Opapa_Roll_Stop",
                false,
                nameof(OpapaCompanion.CompleteRollAttack));

            RuntimeAnimatorController controller = CreateAnimatorController(
                idleClip,
                walkClip,
                blinkClip,
                rollStartClip,
                rollingClip,
                rollStopClip);
            Sprite idleSprite = LoadFrames(sources.idle)[0];
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning(
                    $"[OpapaRelicSetup] 유물 아이콘을 찾을 수 없어 Idle 첫 프레임을 사용합니다: {IconPath}");
                icon = idleSprite;
            }
            GameObject companionPrefab = CreateCompanionPrefab(
                idleSprite,
                controller,
                rollStartClip,
                rollStopClip);
            RelicData relic = CreateOrUpdateRelic(icon, companionPrefab);
            RegisterRelic(relic);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RelicGachaSync.SyncRelicsToGachaPools(false);
            Debug.Log("[OpapaRelicSetup] Opapa 소환수 유물 구성을 완료했습니다.", relic);
        }

        internal static void TryAutomaticSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutomaticSetup;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath) != null ||
                !TryResolveSourceTextures(out _))
            {
                return;
            }

            SetupOpapaRelic();
        }

        private static bool TryResolveSourceTextures(out SourceTextures sources)
        {
            sources = new SourceTextures
            {
                idle = FindTexture("idle", "opapaidle"),
                walk = FindTexture("walk", "opapawalk"),
                blink = FindTexture("blink", "opapablink"),
                rollStart = FindTexture("rollstart", "opaparollstart"),
                rolling = FindTexture("rolling", "opaparolling"),
                rollStop = FindTexture("rollstop", "opaparollstop")
            };

            return !string.IsNullOrEmpty(sources.idle) &&
                   !string.IsNullOrEmpty(sources.walk) &&
                   !string.IsNullOrEmpty(sources.blink) &&
                   !string.IsNullOrEmpty(sources.rollStart) &&
                   !string.IsNullOrEmpty(sources.rolling) &&
                   !string.IsNullOrEmpty(sources.rollStop);
        }

        private static string FindTexture(params string[] acceptedNames)
        {
            if (!AssetDatabase.IsValidFolder(SpriteRoot))
            {
                return null;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string normalizedName = NormalizeName(Path.GetFileNameWithoutExtension(path));
                if (acceptedNames.Any(name => normalizedName == NormalizeName(name)))
                {
                    return path;
                }
            }

            return null;
        }

        private static string NormalizeName(string value)
        {
            return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static AnimationClip CreateClip(
            string texturePath,
            string clipName,
            bool loop,
            string animationEventName)
        {
            ConfigureSpriteSheet(texturePath, clipName);
            Sprite[] frames = LoadFrames(texturePath);
            string clipPath = AnimationRoot + "/" + clipName + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.name = clipName;
            clip.frameRate = FrameRate;
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
                    time = i / FrameRate,
                    value = frames[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AnimationEvent[] events = string.IsNullOrEmpty(animationEventName)
                ? new AnimationEvent[0]
                : new[]
                {
                    new AnimationEvent
                    {
                        functionName = animationEventName,
                        time = Mathf.Max(0f, clip.length - 0.001f)
                    }
                };
            AnimationUtility.SetAnimationEvents(clip, events);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void ConfigureSpriteSheet(string path, string spriteName)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Texture2D sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importer == null || sourceTexture == null)
            {
                throw new MissingReferenceException($"Opapa 스프라이트 원본을 찾을 수 없습니다: {path}");
            }

            Sprite[] existingFrames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            bool hasUserSlices = importer.spriteImportMode == SpriteImportMode.Multiple &&
                                 existingFrames.Length > 1;
            if (hasUserSlices)
            {
                ConfigureTexture(importer, SpriteImportMode.Multiple);
                return;
            }

            int frameCount = sourceTexture.height > 0 && sourceTexture.width % sourceTexture.height == 0
                ? Mathf.Max(1, sourceTexture.width / sourceTexture.height)
                : 1;
            SpriteImportMode importMode = frameCount > 1
                ? SpriteImportMode.Multiple
                : SpriteImportMode.Single;
            ConfigureTexture(importer, importMode);
            if (frameCount <= 1)
            {
                return;
            }

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            ISpriteNameFileIdDataProvider nameFileIdProvider =
                dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            List<SpriteNameFileIdPair> existingNamePairs = nameFileIdProvider
                .GetNameFileIdPairs()
                .ToList();
            int frameWidth = sourceTexture.width / frameCount;
            SpriteRect[] spriteRects = new SpriteRect[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                string frameName = $"{spriteName}_{i:00}";
                SpriteNameFileIdPair existingPair = existingNamePairs
                    .FirstOrDefault(pair => pair.name == frameName);
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
                .OrderBy(sprite => sprite.rect.x)
                .ThenByDescending(sprite => sprite.rect.y)
                .ToArray();
            if (frames.Length == 0)
            {
                throw new MissingReferenceException($"슬라이스된 Opapa 스프라이트가 없습니다: {path}");
            }

            return frames;
        }

        private static RuntimeAnimatorController CreateAnimatorController(
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip blinkClip,
            AnimationClip rollStartClip,
            AnimationClip rollingClip,
            AnimationClip rollStopClip)
        {
            string controllerPath = AnimationRoot + "/Opapa.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Blink", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Roll", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("StopRoll", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = AddState(stateMachine, "Idle", idleClip);
            AnimatorState walkState = AddState(stateMachine, "Walk", walkClip);
            AnimatorState blinkState = AddState(stateMachine, "Blink", blinkClip);
            AnimatorState rollStartState = AddState(stateMachine, "Roll_Start", rollStartClip);
            AnimatorState rollingState = AddState(stateMachine, "Rolling", rollingClip);
            AnimatorState rollStopState = AddState(stateMachine, "Roll_Stop", rollStopClip);
            stateMachine.defaultState = idleState;

            AddBoolTransition(idleState, walkState, true, false);
            AddBoolTransition(walkState, idleState, false, false);
            AddTriggerTransition(idleState, blinkState, "Blink", true);
            AddExitTransition(blinkState, idleState);

            AnimatorStateTransition rollTransition = stateMachine.AddAnyStateTransition(rollStartState);
            rollTransition.hasExitTime = false;
            rollTransition.duration = 0f;
            rollTransition.canTransitionToSelf = false;
            rollTransition.AddCondition(AnimatorConditionMode.If, 0f, "Roll");
            AddExitTransition(rollStartState, rollingState);
            AddTriggerTransition(rollingState, rollStopState, "StopRoll", false);
            AddBoolTransition(rollStopState, walkState, true, true);
            AddBoolTransition(rollStopState, idleState, false, true);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AddState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Motion motion)
        {
            AnimatorState state = stateMachine.AddState(stateName);
            state.motion = motion;
            return state;
        }

        private static void AddBoolTransition(
            AnimatorState from,
            AnimatorState to,
            bool expectedValue,
            bool waitForExit)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = waitForExit;
            transition.exitTime = 1f;
            transition.duration = 0f;
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                "IsWalking");
        }

        private static void AddTriggerTransition(
            AnimatorState from,
            AnimatorState to,
            string triggerName,
            bool waitForExit)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = waitForExit;
            transition.exitTime = 1f;
            transition.duration = 0f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        private static void AddExitTransition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
        }

        private static GameObject CreateCompanionPrefab(
            Sprite idleSprite,
            RuntimeAnimatorController controller,
            AnimationClip rollStartClip,
            AnimationClip rollStopClip)
        {
            GameObject root = new GameObject("Opapa");
            Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.drag = 0f;
            rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Clamp(
                Mathf.Max(idleSprite.bounds.extents.x, idleSprite.bounds.extents.y) * 0.7f,
                0.2f,
                0.6f);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = idleSprite;
            Animator animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            visual.AddComponent<OpapaAnimationEventRelay>();

            OpapaCompanion companion = root.AddComponent<OpapaCompanion>();
            SerializedObject serializedCompanion = new SerializedObject(companion);
            SetObjectReference(serializedCompanion, "visual", visual);
            SetFloat(serializedCompanion, "followOffsetX", 1.2f);
            SetFloat(serializedCompanion, "followOffsetY", 0.55f);
            SetFloat(serializedCompanion, "wakeupDistance", 0.8f);
            SetFloat(serializedCompanion, "stopRadius", 0.35f);
            SetFloat(serializedCompanion, "leashRange", 36f);
            SetFloat(serializedCompanion, "minMoveSpeed", 3.5f);
            SetFloat(serializedCompanion, "maxMoveSpeed", 4.5f);
            SetFloat(serializedCompanion, "acceleration", 15f);
            SetFloat(serializedCompanion, "attackRange", 8f);
            SetFloat(serializedCompanion, "attackInterval", 5.5f);
            SetFloat(serializedCompanion, "attackFreezeDuration", 0f);
            SetFloat(serializedCompanion, "baseDamage", 8f);
            SetFloat(serializedCompanion, "weaponDamageRatio", 0.65f);
            SetFloat(serializedCompanion, "damageRatioPerLevel", 0.12f);
            SetString(serializedCompanion, "walkingBoolParam", "IsWalking");
            SetString(serializedCompanion, "jumpTriggerParam", string.Empty);
            SetString(serializedCompanion, "attackTriggerParam", "Roll");
            SetString(serializedCompanion, "alternateAttackTriggerParam", string.Empty);

            SetFloat(serializedCompanion, "rollingSpeedMultiplier", 2.25f);
            SetFloat(serializedCompanion, "rollingDuration", 3f);
            SetFloat(serializedCompanion, "rollStartFallbackDuration", Mathf.Max(0.05f, rollStartClip.length));
            SetFloat(serializedCompanion, "rollStopFallbackDuration", Mathf.Max(0.05f, rollStopClip.length));
            SetFloat(serializedCompanion, "collisionSkin", 0.02f);
            serializedCompanion.FindProperty("blockingLayers").intValue =
                LayerMask.GetMask("Wall", "Obstacle");
            serializedCompanion.FindProperty("maximumBouncesPerStep").intValue = 4;
            SetString(serializedCompanion, "blinkTriggerParam", "Blink");
            SetString(serializedCompanion, "rollStopTriggerParam", "StopRoll");
            SetFloat(serializedCompanion, "minimumBlinkInterval", 3f);
            SetFloat(serializedCompanion, "maximumBlinkInterval", 7f);
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

            relic.name = "Opapa";
            relic.relicName = "Opapa";
            relic.koreanName = "오파파";
            relic.description_KR =
                "적을 향해 돌진한 뒤 벽, 장애물과 화면 경계에서 튕기며 적을 관통하는 Opapa를 소환합니다.\n" +
                "구르기에 닿은 적은 한 번씩 피해를 받습니다.";
            relic.description_EN =
                "Summons Opapa, who charges toward enemies, pierces them, and ricochets from walls, " +
                "obstacles, and the camera boundary.";
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
                    description_KR = "구르기 동안 적을 관통하고 지형과 화면 경계에서 반사됩니다.",
                    description_EN = "Pierces enemies while rolling and ricochets from terrain and screen bounds.",
                    condition = new AlwaysTrueCondition(),
                    effects = new List<RelicEffectBase>
                    {
                        new SummonedCompanionRelicEffect
                        {
                            companionPrefab = companionPrefab,
                            spawnOffset = new Vector3(-1.2f, 0.55f, 0f)
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
                Debug.LogError("[OpapaRelicSetup] RelicDatabase 또는 Opapa 유물을 찾을 수 없습니다.");
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

    /// <summary>
    /// Opapa 원본 이미지가 나중에 추가되는 경우에도 셋업을 자동 실행합니다.
    /// </summary>
    internal sealed class OpapaRelicAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Any(path => path.StartsWith(OpapaRelicSetup.SpriteRoot)) ||
                movedAssets.Any(path => path.StartsWith(OpapaRelicSetup.SpriteRoot)))
            {
                EditorApplication.delayCall += OpapaRelicSetup.TryAutomaticSetup;
            }
        }
    }
}
