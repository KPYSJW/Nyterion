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
    /// 유령 원본 시트를 슬라이스하고 배회 소환수 프리팹을 구성합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class GhostCompanionSetup
    {
        private const string ArtRoot = "Assets/Nytherion/Art/Characters/Companions/Ghost";
        private const string AnimationRoot = ArtRoot + "/Animations";
        private const string SpriteRoot = ArtRoot + "/Sprites";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Characters/Companions/GhostCompanion.prefab";
        private const string IconPath = "Assets/Nytherion/Art/Relics/Sprites/Ghost.png";
        private const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Ghost.asset";
        private const string DatabasePath = "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset";
        private const string HitPrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/GhostSpiritHitVFX.prefab";
        private const float FrameRate = 10f;

        static GhostCompanionSetup()
        {
            EditorApplication.delayCall += TryAutomaticSetup;
        }

        private static void TryAutomaticSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryAutomaticSetup;
                return;
            }

            if ((AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null ||
                 AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath) == null) &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath) != null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteRoot + "/Ghost_Idle.png") != null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteRoot + "/Ghost_Attack.png") != null)
            {
                Setup();
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GhostCompanion companion = prefab != null ? prefab.GetComponent<GhostCompanion>() : null;
            if (companion != null &&
                new SerializedObject(companion).FindProperty("spiritHitEffectPrefab").objectReferenceValue == null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteRoot + "/Ghost_SpiritHit.png") != null)
            {
                SetupAttackEffect();
            }
        }

        [MenuItem("Nytherion/Setup Ghost Companion")]
        public static void Setup()
        {
            EnsureFolder(AnimationRoot);
            Sprite[] idleFrames = ConfigureFrames("Ghost_Idle", 5);
            Sprite[] attackFrames = ConfigureFrames("Ghost_Attack", 12);
            AnimationClip idleClip = CreateClip("Ghost_Idle", idleFrames, true);
            AnimationClip attackClip = CreateClip("Ghost_Attack", attackFrames, false);
            AnimationUtility.SetAnimationEvents(attackClip, new[]
            {
                new AnimationEvent { time = 4f / FrameRate, functionName = "ReleaseSpiritHit" },
                new AnimationEvent { time = 8f / FrameRate, functionName = "EndSpiritHit" }
            });
            EditorUtility.SetDirty(attackClip);

            string controllerPath = AnimationRoot + "/Ghost.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                AnimatorState idle = machine.AddState("Idle");
                idle.motion = idleClip;
                AnimatorState attack = machine.AddState("Attack");
                attack.motion = attackClip;
                machine.defaultState = idle;
                AnimatorStateTransition start = idle.AddTransition(attack);
                start.hasExitTime = false;
                start.duration = 0f;
                start.AddCondition(AnimatorConditionMode.If, 0f, "Attack1");
                AnimatorStateTransition end = attack.AddTransition(idle);
                end.hasExitTime = true;
                end.exitTime = 1f;
                end.duration = 0f;
                EditorUtility.SetDirty(controller);
            }

            // 기존 프리팹의 Inspector 설정과 연결된 공격 기능은 재실행해도 보존합니다.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                GameObject instance = new GameObject("GhostCompanion");
                try
                {
                    SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
                    renderer.sprite = idleFrames[0];
                    instance.AddComponent<Animator>().runtimeAnimatorController = controller;
                    Rigidbody2D body = instance.AddComponent<Rigidbody2D>();
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.gravityScale = 0f;
                    body.constraints = RigidbodyConstraints2D.FreezeRotation;
                    body.interpolation = RigidbodyInterpolation2D.Interpolate;
                    GhostCompanion companion = instance.AddComponent<GhostCompanion>();
                    SerializedObject settings = new SerializedObject(companion);
                    settings.FindProperty("attackInterval").floatValue = 2f;
                    settings.FindProperty("attackFreezeDuration").floatValue = attackFrames.Length / FrameRate;
                    settings.FindProperty("alternateAttackTriggerParam").stringValue = string.Empty;
                    settings.FindProperty("walkingBoolParam").stringValue = string.Empty;
                    settings.FindProperty("projectilePoolTag").stringValue = string.Empty;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            SetupRelic();
            AssetDatabase.SaveAssets();
            RelicGachaSync.SyncRelicsToGachaPools(false);
            Debug.Log("[GhostCompanionSetup] 유령 소환 유물과 아이콘·가챠 등록을 완료했습니다. 실제 공격 기능은 별도로 연결하세요.");
        }

        private static void SetupRelic()
        {
            TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(DatabasePath);
            if (importer == null || prefab == null || database == null)
            {
                throw new MissingReferenceException("유령 아이콘, 소환수 프리팹 또는 유물 데이터베이스가 없습니다.");
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Ghost";
                relic.koreanName = "유령";
                relic.englishName = "Ghost";
                relic.description_KR = "장착 중 플레이어 주변을 떠다니는 유령을 소환합니다.";
                relic.description_EN = "While equipped, summons a ghost that floats around the player.";
                relic.rarity = Rarity.Epic;
                relic.level = 1;
                relic.shape = new List<Vector2Int> { Vector2Int.zero };
                relic.synergySeriesId = "SummonedCreatureSeries";
                relic.synergySetBonusData = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(
                    "Assets/Nytherion/Data/ScriptableObjects/Relics/SummonedCreatureSet.asset");
                relic.effectModules = new List<RelicEffectModule>
                {
                    new RelicEffectModule
                    {
                        description_KR = relic.description_KR,
                        description_EN = relic.description_EN,
                        condition = new AlwaysTrueCondition(),
                        effects = new List<RelicEffectBase>
                        {
                            new SummonedCompanionRelicEffect { companionPrefab = prefab }
                        }
                    }
                };
                RelicInfluencePolicy.Normalize(relic);
                AssetDatabase.CreateAsset(relic, RelicPath);
            }

            // 재실행 시 기존 밸런스와 공격 설정을 보존하고 아이콘·등록만 갱신합니다.
            relic.Image = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            EditorUtility.SetDirty(relic);
            if (database.allRelics == null) database.allRelics = new List<RelicData>();
            if (!database.allRelics.Contains(relic))
            {
                database.allRelics.Add(relic);
                EditorUtility.SetDirty(database);
            }
        }

        private static Sprite[] ConfigureFrames(string name, int count)
        {
            string path = SpriteRoot + "/" + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importer == null || texture == null)
            {
                throw new MissingReferenceException("유령 원본 이미지를 찾을 수 없습니다: " + path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] existing = provider.GetSpriteRects();
            int columns = texture.width / 32;
            if (columns == 0 || count > columns * (texture.height / 32))
            {
                throw new UnityException("유령 프레임 수가 원본 시트 크기를 초과합니다: " + path);
            }
            SpriteRect[] rects = new SpriteRect[count];
            for (int i = 0; i < count; i++)
            {
                string frameName = name + "_" + i.ToString("00");
                SpriteRect previous = existing.FirstOrDefault(rect => rect.name == frameName);
                rects[i] = new SpriteRect
                {
                    name = frameName,
                    rect = new Rect(i % columns * 32, texture.height - (i / columns + 1) * 32, 32, 32),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = previous != null ? previous.spriteID : GUID.Generate()
                };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        }

        private static AnimationClip CreateClip(string name, Sprite[] frames, bool loop)
        {
            string path = AnimationRoot + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.frameRate = FrameRate;
            // 마지막 프레임도 온전히 한 프레임 동안 재생합니다.
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / FrameRate,
                    value = frames[Mathf.Min(i, frames.Length - 1)]
                };
            }
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        [MenuItem("Nytherion/Setup Ghost Attack Effect")]
        public static void SetupAttackEffect()
        {
            EnsureFolder(AnimationRoot);
            Sprite[] frames = ConfigureFrames("Ghost_SpiritHit", 6);
            AnimationClip clip = CreateClip("Ghost_SpiritHit", frames, false);
            string controllerPath = AnimationRoot + "/GhostSpiritHit.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                AnimatorState state = controller.layers[0].stateMachine.AddState("SpiritHit");
                state.motion = clip;
                controller.layers[0].stateMachine.defaultState = state;
                EditorUtility.SetDirty(controller);
            }

            GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitPrefabPath);
            if (effectPrefab == null)
            {
                EnsureFolder("Assets/Prefabs/Gameplay/Combat/VFX");
                GameObject instance = new GameObject("GhostSpiritHitVFX");
                try
                {
                    instance.transform.localScale = Vector3.one * 0.5f;
                    instance.AddComponent<SpriteRenderer>().sprite = frames[0];
                    Animator animator = instance.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    instance.AddComponent<AutoReturnToPool>();
                    effectPrefab = PrefabUtility.SaveAsPrefabAsset(instance, HitPrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                SerializedObject settings = new SerializedObject(contents.GetComponent<GhostCompanion>());
                if (settings.FindProperty("spiritHitEffectPrefab").objectReferenceValue == null)
                {
                    settings.FindProperty("spiritHitEffectPrefab").objectReferenceValue = effectPrefab;
                    settings.FindProperty("spiritHitEffectLifetime").floatValue = 4f / FrameRate;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[GhostCompanionSetup] 유령 영혼 적중 이펙트를 연결했습니다.");
        }
    }
}
