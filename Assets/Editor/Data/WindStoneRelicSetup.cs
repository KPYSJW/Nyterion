using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Gameplay.Relics.Modules;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class WindStoneRelicSetup
    {
        public const string Output = "output/wind-stone";
        public const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/CombatUtility/WindStone.asset";
        public const string ProjectilePath = "Assets/Prefabs/Gameplay/Combat/Proj/WindStoneProj.prefab";
        public const string HitPath = "Assets/Prefabs/Gameplay/Combat/VFX/WindStoneProjHitEffect.prefab";
        public const string AnimationFolder = "Assets/Nytherion/Art/Combat/VFX/Animations/WindStone";
        private const string SpriteFolder = "Assets/Nytherion/Art/Combat/VFX/Sprites/";

        static WindStoneRelicSetup() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { CreateAssets(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Wind Stone/Create Relic")]
        public static void CreateAssets()
        {
            Directory.CreateDirectory(Output);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            EnsureFolder(AnimationFolder);
            Sprite[] frames = LoadFrames(SpriteFolder + "WindStoneProj.png", 5);
            Sprite[] hitFrames = LoadFrames(SpriteFolder + "WindStoneProjHitEffect.png", 4);
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Relics/Sprites/WindStone.png");
            if (icon == null) throw new InvalidOperationException("WindStone.png 아이콘 누락");
            var loop = CreateAnimation("WindStoneProj", frames, true);
            var hitAnimation = CreateAnimation("WindStoneProjHitEffect", hitFrames, false);

            var hitRoot = new GameObject("WindStoneProjHitEffect");
            GameObject hitPrefab;
            try
            {
                AddVisual(hitRoot, hitFrames[0], hitAnimation);
                hitRoot.AddComponent<AutoReturnToPool>();
                hitPrefab = PrefabUtility.SaveAsPrefabAsset(hitRoot, HitPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(hitRoot); }

            var root = new GameObject("WindStoneProj");
            GameObject projectile;
            try
            {
                AddVisual(root, frames[0], loop);
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var collider = root.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.3f;
                var collision = root.AddComponent<CollisionObject>();
                collision.poolTag = "WindStoneProj";
                collision.hitEffectPrefab = hitPrefab;
                collision.isChainDamage = true;
                var multiHit = root.AddComponent<SlowMultiHitModifier>();
                multiHit.slowSpeed = 1f;
                multiHit.tickRate = 0.25f;
                multiHit.lifeTime = 1.5f;
                multiHit.playHitEffectOnTick = true;
                var lifetime = new SerializedObject(root.AddComponent<AutoReturnToPool>());
                lifetime.FindProperty("returnDelay").floatValue = 6f;
                lifetime.ApplyModifiedPropertiesWithoutUndo();
                projectile = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                AssetDatabase.CreateAsset(relic, RelicPath);
            }
            relic.relicName = "WindStone";
            relic.koreanName = "풍석";
            relic.description_KR = "공격 적중 시 20% 확률로 플레이어 주변에서 공격 방향으로 풍석을 발사합니다. 풍석은 적과 충돌하면 느려지며 0.25초마다 반복 타격합니다. 타격 피해는 적중 피해의 50%이며 유물 레벨마다 10%p 증가합니다.";
            relic.description_EN = "Hits have a 20% chance to launch a Wind Stone around the player in the player's attack direction. It slows on contact and hits every 0.25 seconds, dealing 50% of the triggering damage (+10 percentage points per relic level).";
            relic.Image = icon;
            relic.rarity = Rarity.Rare;
            relic.level = 1;
            relic.isDisabled = false;
            relic.effectModules = new List<RelicEffectModule>
            {
                new RelicEffectModule
                {
                    description_KR = relic.description_KR,
                    description_EN = relic.description_EN,
                    effects = new List<RelicEffectBase> { new WindStoneRelicEffect { projectilePrefab = projectile } }
                }
            };
            RelicInfluencePolicy.Normalize(relic);
            EditorUtility.SetDirty(relic);
            var database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset");
            if (database == null) throw new InvalidOperationException("유물 데이터베이스 누락");
            if (!database.allRelics.Contains(relic)) { database.allRelics.Add(relic); EditorUtility.SetDirty(database); }
            AssetDatabase.SaveAssets();
            RelicGachaSync.SyncRelicsToGachaPools(false);
            File.WriteAllText(Output + "/setup.txt", "PASS: 풍석 유물/DB/가챠 등록, 5프레임 루프, 4프레임 타격, SlowMultiHit 연결");
        }

        private static Sprite[] LoadFrames(string path, int expected)
        {
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            if (frames.Length != expected) throw new InvalidOperationException(path + " 프레임 수 확인 필요: " + frames.Length);
            return frames;
        }

        private static RuntimeAnimatorController CreateAnimation(string name, Sprite[] frames, bool loop)
        {
            string path = AnimationFolder + "/" + name;
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path + ".anim");
            if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path + ".anim"); }
            clip.ClearCurves();
            clip.frameRate = 12f;
            // Unity는 마지막 스프라이트 키 뒤에 한 프레임을 자동으로 유지합니다.
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path + ".controller");
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path + ".controller");
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.FirstOrDefault(s => s.state.name == name).state;
            if (state == null) state = machine.AddState(name);
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(clip);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddVisual(GameObject root, Sprite sprite, RuntimeAnimatorController controller)
        {
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 10;
            root.AddComponent<Animator>().runtimeAnimatorController = controller;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
