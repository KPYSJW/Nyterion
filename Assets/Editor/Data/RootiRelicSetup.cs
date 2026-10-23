using System.Collections.Generic;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Companions;
using Nytherion.GamePlay.Skills;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>
    /// 기존 루티 에셋의 GUID와 크기를 유지하며 소환수에서 포탑 스킬로 전환합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class RootiRelicSetup
    {
        private const string ArtRoot = "Assets/Nytherion/Art/Characters/Companions/Rooti";
        private const string AnimationRoot = ArtRoot + "/Animations";
        private const string SpriteRoot = ArtRoot + "/Sprites";
        private const string SourceRootiPrefabPath =
            "Assets/Prefabs/Gameplay/Characters/Companions/Rooti/Rooti.prefab";
        private const string RootiPrefabPath = "Assets/Prefabs/Gameplay/Skills/RootiTurret.prefab";
        private const string ProjectilePrefabPath =
            "Assets/Prefabs/Gameplay/Combat/Proj/RootiSeedProjectile.prefab";
        private const string SkillPath = "Assets/Nytherion/Data/ScriptableObjects/Skill/Turret_Skill.asset";
        private const string LegacyRelicPath =
            "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Rooti.asset";
        private const float FrameRate = 10f;

        static RootiRelicSetup()
        {
            EditorApplication.delayCall += TryAutomaticSetup;
        }

        [MenuItem("Nytherion/Setup Rooti Turret Skill")]
        public static void SetupRootiTurretSkill()
        {
            TurretSkillData skill = AssetDatabase.LoadAssetAtPath<TurretSkillData>(SkillPath);
            GameObject rootiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourceRootiPrefabPath);
            GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            if (skill == null || rootiPrefab == null || projectilePrefab == null)
            {
                Debug.LogError("[RootiRelicSetup] 기존 포탑 스킬, 루티 또는 씨앗 프리팹이 없습니다.");
                return;
            }

            Sprite[] jumpFrames = ConfigureAndLoadFrames(SpriteRoot + "/Rooti_Jump.png", "Rooti_Jump", 1);
            Sprite[] landedFrames = ConfigureAndLoadFrames(SpriteRoot + "/Rooti_Landed.png", "Rooti_Landed", 2);
            AnimationClip jumpClip = CreateClip(AnimationRoot + "/Rooti_Jump.anim", jumpFrames, true);
            AnimationClip landedClip = CreateClip(
                AnimationRoot + "/Rooti_Landed.anim",
                landedFrames,
                false,
                nameof(RootiTurretAnimationEventRelay.CompleteLanding));
            AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationRoot + "/Rooti_Idle.anim");
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationRoot + "/Rooti_FlowerWhipAttack.anim");
            if (idleClip == null || attackClip == null)
            {
                Debug.LogError("[RootiRelicSetup] 루티의 기존 Idle 또는 꽃채찍 공격 애니메이션이 없습니다.");
                return;
            }

            AnimatorController controller = ConfigureController(idleClip, jumpClip, landedClip, attackClip);
            ConfigureRootiPrefab(controller, landedClip.length, attackClip.length);

            // 스킬 ID, 쿨다운, 피해량, 사거리, 유지시간과 최대 개수는 기존 값을 보존합니다.
            skill.turretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RootiPrefabPath);
            skill.launchAroundCaster = true;
            skill.minimumDeploymentDistance = Mathf.Min(0.6f, Mathf.Max(0.1f, skill.searchRadius));
            skill.projectilePrefab = projectilePrefab;
            skill.projectilePoolTag = projectilePrefab.name;
            skill.projectileSpawnOffset = new Vector3(0f, 0.46f, 0f);
            skill.skillName = "루티 포탑";
            skill.description = "루티가 플레이어의 조준 방향으로 날아가 착지한 뒤, 제자리에서 공격 범위 내의 적에게 씨앗을 발사합니다.";
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Relics/Sprites/Rooti.png");
            if (icon != null) skill.icon = icon;
            EditorUtility.SetDirty(skill);

            RetireLegacyRelic();
            AssetDatabase.SaveAssets();
            Debug.Log("[RootiRelicSetup] 루티를 Jump → Landed → Idle 순서로 배치하는 포탑 스킬로 전환했습니다.", skill);
        }

        private static void TryAutomaticSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryAutomaticSetup;
                return;
            }

            TurretSkillData skill = AssetDatabase.LoadAssetAtPath<TurretSkillData>(SkillPath);
            if (skill == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteRoot + "/Rooti_Jump.png") == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteRoot + "/Rooti_Landed.png") == null)
            {
                return;
            }
            if (!skill.launchAroundCaster || skill.turretPrefab == null || skill.turretPrefab.name != "RootiTurret" ||
                AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationRoot + "/Rooti_Landed.anim") == null)
            {
                SetupRootiTurretSkill();
            }
        }

        private static Sprite[] ConfigureAndLoadFrames(string path, string spriteName, int frameCount)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importer == null || texture == null)
            {
                throw new MissingReferenceException($"루티 배치 스프라이트를 찾을 수 없습니다: {path}");
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            ISpriteNameFileIdDataProvider nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            List<SpriteNameFileIdPair> existingPairs = nameProvider.GetNameFileIdPairs().ToList();
            SpriteRect[] rects = new SpriteRect[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                string frameName = $"{spriteName}_{i:00}";
                SpriteNameFileIdPair pair = existingPairs.FirstOrDefault(item => item.name == frameName);
                rects[i] = new SpriteRect
                {
                    name = frameName,
                    rect = new Rect(i * 32f, 0f, 32f, 32f),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = pair != null ? pair.GetFileGUID() : GUID.Generate()
                };
            }
            nameProvider.SetNameFileIdPairs(Enumerable.Empty<SpriteNameFileIdPair>());
            provider.SetSpriteRects(rects);
            nameProvider.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
        }

        private static AnimationClip CreateClip(string path, Sprite[] frames, bool loop, string completionEvent = null)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
            clip.frameRate = FrameRate;
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };
            ObjectReferenceKeyframe[] keyframes = frames.Select((sprite, index) =>
                new ObjectReferenceKeyframe { time = index / FrameRate, value = sprite }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, string.IsNullOrEmpty(completionEvent)
                ? System.Array.Empty<AnimationEvent>()
                : new[]
                {
                    new AnimationEvent
                    {
                        functionName = completionEvent,
                        time = Mathf.Max(0f, frames.Length / FrameRate - 0.001f)
                    }
                });
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController ConfigureController(
            AnimationClip idleClip, AnimationClip jumpClip, AnimationClip landedClip, AnimationClip attackClip)
        {
            string path = AnimationRoot + "/RootiTurret.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                machine.RemoveAnyStateTransition(transition);
            }
            foreach (ChildAnimatorState child in machine.states)
            {
                foreach (AnimatorStateTransition transition in child.state.transitions)
                {
                    child.state.RemoveTransition(transition);
                }
                if (child.state.name == "Walk") machine.RemoveState(child.state);
            }
            controller.parameters = System.Array.Empty<AnimatorControllerParameter>();
            machine.defaultState = SetState(machine, "Idle", idleClip);
            SetState(machine, "Jump", jumpClip);
            SetState(machine, "Landed", landedClip);
            SetState(machine, "Flower Whip Attack", attackClip);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState SetState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(item => item.name == name);
            if (state == null) state = machine.AddState(name);
            state.motion = clip;
            EditorUtility.SetDirty(state);
            return state;
        }

        private static void ConfigureRootiPrefab(AnimatorController controller, float landedDuration, float attackDuration)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SourceRootiPrefabPath);
            try
            {
                RootiCompanion legacyCompanion = root.GetComponent<RootiCompanion>();
                if (legacyCompanion != null) Object.DestroyImmediate(legacyCompanion);
                RootiAnimationEventRelay legacyRelay = root.GetComponentInChildren<RootiAnimationEventRelay>();
                if (legacyRelay != null) Object.DestroyImmediate(legacyRelay);
                RootiTurretController rooti = root.AddComponent<RootiTurretController>();
                Animator animator = root.GetComponentInChildren<Animator>();
                if (rooti == null || animator == null)
                {
                    throw new MissingReferenceException("루티 프리팹의 포탑 또는 Animator 컴포넌트가 없습니다.");
                }
                root.name = "RootiTurret";
                animator.runtimeAnimatorController = controller;
                animator.gameObject.AddComponent<RootiTurretAnimationEventRelay>();
                SerializedObject serialized = new SerializedObject(rooti);
                serialized.FindProperty("visual").objectReferenceValue = animator.gameObject;
                serialized.FindProperty("landingDuration").floatValue = Mathf.Max(0.1f, landedDuration);
                serialized.FindProperty("attackAnimationDuration").floatValue = Mathf.Max(0.1f, attackDuration);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (root.TryGetComponent(out Rigidbody2D rigidbody))
                {
                    rigidbody.bodyType = RigidbodyType2D.Kinematic;
                    rigidbody.gravityScale = 0f;
                    rigidbody.drag = 0f;
                }
                PrefabUtility.SaveAsPrefabAsset(root, RootiPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RetireLegacyRelic()
        {
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(LegacyRelicPath);
            if (relic == null) return;

            // 구형 ID와 GUID는 보존하고 현재 루티와 혼동되는 표시 및 획득 참조를 정리합니다.
            relic.koreanName = "루티 (사용 중단)";
            relic.englishName = "Rooti (Retired)";
            relic.Image = null;
            relic.effectModules = new List<RelicEffectModule>();
            relic.isDisabled = true;
            relic.synergySeriesId = string.Empty;
            relic.synergySetBonusData = null;
            relic.description_KR = "사용이 중단된 구형 루티 유물입니다.";
            relic.description_EN = "This legacy Rooti relic has been retired.";
            EditorUtility.SetDirty(relic);
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset");
            if (database != null && database.allRelics != null && database.allRelics.RemoveAll(item => item == relic) > 0)
            {
                EditorUtility.SetDirty(database);
            }
            GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Relic/Epic_Relic.asset");
            if (pool != null && pool.items != null)
            {
                if (pool.items.RemoveAll(rate => rate != null && rate.item == relic) > 0)
                {
                    EditorUtility.SetDirty(pool);
                }
            }
        }
    }
}
