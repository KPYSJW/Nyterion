using System.Collections.Generic;
using System.Linq;
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
    /// <summary>첨부 시트로 눈사람 소환수를 만들고 기존 유물에 연결합니다.</summary>
    [InitializeOnLoad]
    public static class SnowmanCompanionSetup
    {
        private const string ArtRoot = "Assets/Nytherion/Art/Characters/Companions/Snowman";
        private const string AnimationRoot = ArtRoot + "/Animations";
        private const string SpriteRoot = ArtRoot + "/Sprites";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Characters/Companions/SnowmanCompanion.prefab";
        private const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/Elemental/SnowMan.asset";
        private const string HitPrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/SnowmanFrostHitVFX.prefab";
        private const float FrameRate = 10f;

        static SnowmanCompanionSetup()
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

            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (relic == null) return;
            if (prefab != null && relic.effectModules != null &&
                relic.effectModules.Any(module => module.effects != null && module.effects.Any(
                    effect => effect is SummonedCompanionRelicEffect summon && summon.companionPrefab == prefab)))
            {
                SnowmanCompanion companion = prefab.GetComponent<SnowmanCompanion>();
                if (companion != null && new SerializedObject(companion).FindProperty("frostHitEffectPrefab").objectReferenceValue == null &&
                    AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteRoot + "/Snowman_FrostHit.png") != null)
                {
                    SetupAttackEffect();
                    AssetDatabase.SaveAssets();
                }
                return;
            }

            if (new[] { "Idle", "Walk", "Attack", "Dismiss" }.All(name =>
                AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteRoot + "/Snowman_" + name + ".png") != null))
            {
                Setup();
            }
        }

        [MenuItem("Nytherion/Setup Snowman Companion")]
        public static void Setup()
        {
            EnsureFolder(AnimationRoot);
            Sprite[] idle = ConfigureFrames("Idle", 7);
            AnimationClip idleClip = CreateClip("Idle", idle, true);
            AnimationClip walkClip = CreateClip("Walk", ConfigureFrames("Walk", 11), true);
            AnimationClip attackClip = CreateClip("Attack", ConfigureFrames("Attack", 10), false);
            AnimationUtility.SetAnimationEvents(attackClip, new[]
            {
                new AnimationEvent { time = 5f / FrameRate, functionName = "ReleaseFrost" }
            });
            AnimationClip dismissClip = CreateClip("Dismiss", ConfigureFrames("Dismiss", 17), false);

            string controllerPath = AnimationRoot + "/Snowman.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("Dismiss", AnimatorControllerParameterType.Trigger);
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                AnimatorState rest = machine.AddState("Idle");
                rest.motion = idleClip;
                AnimatorState walk = machine.AddState("Walk");
                walk.motion = walkClip;
                AnimatorState attack = machine.AddState("Attack");
                attack.motion = attackClip;
                AnimatorState dismiss = machine.AddState("Dismiss");
                dismiss.motion = dismissClip;
                machine.defaultState = rest;
                Transition(rest, walk).AddCondition(AnimatorConditionMode.If, 0f, "IsWalking");
                Transition(walk, rest).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWalking");
                Transition(rest, attack).AddCondition(AnimatorConditionMode.If, 0f, "Attack1");
                Transition(walk, attack).AddCondition(AnimatorConditionMode.If, 0f, "Attack1");
                AnimatorStateTransition finish = Transition(attack, rest);
                finish.hasExitTime = true;
                finish.exitTime = 1f;
                AnimatorStateTransition leave = machine.AddAnyStateTransition(dismiss);
                leave.duration = 0f;
                leave.hasExitTime = false;
                leave.canTransitionToSelf = false;
                leave.AddCondition(AnimatorConditionMode.If, 0f, "Dismiss");
                EditorUtility.SetDirty(controller);
            }

            // 재실행 시 기존 Inspector 조정값을 유지합니다.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EnsureFolder("Assets/Prefabs/Gameplay/Characters/Companions");
                GameObject instance = new GameObject("SnowmanCompanion");
                try
                {
                    instance.AddComponent<SpriteRenderer>().sprite = idle[0];
                    Animator animator = instance.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    Rigidbody2D body = instance.AddComponent<Rigidbody2D>();
                    body.gravityScale = 0f;
                    body.constraints = RigidbodyConstraints2D.FreezeRotation;
                    body.interpolation = RigidbodyInterpolation2D.Interpolate;
                    SnowmanCompanion companion = instance.AddComponent<SnowmanCompanion>();
                    SerializedObject settings = new SerializedObject(companion);
                    settings.FindProperty("attackRange").floatValue = 6f;
                    settings.FindProperty("attackInterval").floatValue = 4f;
                    settings.FindProperty("attackFreezeDuration").floatValue = attackClip.length;
                    settings.FindProperty("invertFacing").boolValue = false;
                    settings.FindProperty("alternateAttackTriggerParam").stringValue = string.Empty;
                    settings.FindProperty("projectilePoolTag").stringValue = string.Empty;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            SetupAttackEffect();
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null) throw new MissingReferenceException("눈사람 유물 데이터를 찾을 수 없습니다.");
            if (relic.effectModules == null) relic.effectModules = new List<RelicEffectModule>();
            if (!relic.effectModules.Any(module => module.effects != null && module.effects.Any(
                effect => effect is SummonedCompanionRelicEffect summon && summon.companionPrefab == prefab)))
            {
                relic.effectModules.Add(new RelicEffectModule
                {
                    description_KR = "플레이어를 따라다니며 가까운 적에게 냉기를 내뿜는 눈사람을 소환합니다.",
                    description_EN = "Summons a snowman that follows the player and chills nearby enemies.",
                    condition = new AlwaysTrueCondition(),
                    effects = new List<RelicEffectBase>
                    {
                        new SummonedCompanionRelicEffect { companionPrefab = prefab }
                    }
                });
            }
            relic.description_KR = "냉기로 공격하는 눈사람을 소환하고 빙결 장비 장착 시 방어력과 최대 체력, 피해량을 높입니다.";
            relic.description_EN = "Summons a frost attacking snowman and boosts defense, max health and damage with Ice equipment.";
            EditorUtility.SetDirty(relic);
            AssetDatabase.SaveAssets();
            Debug.Log("[SnowmanCompanionSetup] 눈사람 시트·애니메이션·프리팹과 유물 연결을 완료했습니다.", relic);
        }

        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            return transition;
        }

        private static Sprite[] ConfigureFrames(string state, int count, int cellSize = 32)
        {
            string name = "Snowman_" + state;
            string path = SpriteRoot + "/" + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importer == null || texture == null) throw new MissingReferenceException(path);
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
            SpriteRect[] rects = new SpriteRect[count];
            int columns = texture.width / cellSize;
            for (int i = 0; i < count; i++)
            {
                string frameName = name + "_" + i.ToString("00");
                SpriteRect previous = existing.FirstOrDefault(rect => rect.name == frameName);
                rects[i] = new SpriteRect
                {
                    name = frameName,
                    rect = new Rect(i % columns * cellSize, texture.height - (i / columns + 1) * cellSize, cellSize, cellSize),
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

        private static AnimationClip CreateClip(string state, Sprite[] frames, bool loop)
        {
            string path = AnimationRoot + "/Snowman_" + state + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.frameRate = FrameRate;
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / FrameRate, value = frames[Mathf.Min(i, frames.Length - 1)] };
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
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static void SetupAttackEffect()
        {
            EnsureFolder(AnimationRoot);
            Sprite[] frames = ConfigureFrames("FrostHit", 4, 16);
            AnimationClip clip = CreateClip("FrostHit", frames, false);
            string controllerPath = AnimationRoot + "/SnowmanFrostHit.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                AnimatorState state = controller.layers[0].stateMachine.AddState("FrostHit");
                state.motion = clip;
                controller.layers[0].stateMachine.defaultState = state;
                EditorUtility.SetDirty(controller);
            }

            GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitPrefabPath);
            if (effectPrefab == null)
            {
                EnsureFolder("Assets/Prefabs/Gameplay/Combat/VFX");
                GameObject instance = new GameObject("SnowmanFrostHitVFX");
                try
                {
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
                SerializedObject settings = new SerializedObject(contents.GetComponent<SnowmanCompanion>());
                if (settings.FindProperty("frostHitEffectPrefab").objectReferenceValue == null)
                {
                    settings.FindProperty("frostHitEffectPrefab").objectReferenceValue = effectPrefab;
                    settings.FindProperty("frostHitEffectLifetime").floatValue = clip.length;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            Debug.Log("[SnowmanCompanionSetup] 눈사람 냉기 적중 이펙트를 연결했습니다.");
        }
    }
}
