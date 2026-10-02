using System;
using System.IO;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class TargetMakerSetup
    {
        public const string Output = "output/target-maker";
        public const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/CombatUtility/TargetMaker.asset";
        public const string SummonPath = "Assets/Prefabs/Gameplay/Combat/VFX/BlazeShadeSummonEffect.prefab";
        public const string AttackPath = "Assets/Prefabs/Gameplay/Combat/VFX/BlazeShadeAttackEffect.prefab";
        private const string AnimationFolder = "Assets/Nytherion/Art/Combat/Weapons/Animations/Blazeshade/";
        private const string SpriteFolder = "Assets/Nytherion/Art/Combat/VFX/Sprites/";
        private const string DescriptionKR = "블레이즈셰이드와 수호자의 지팡이의 원형 공격 중심을 마우스 지정 위치로 변경합니다. 블레이즈셰이드는 소환 후 불꽃 고리를 한 번 펼치며 적마다 한 번 타격합니다. 연쇄 점화는 조준 지점을 중심으로 3단계 확산 폭발하며, 첫 단계에만 중심점에서도 1회 추가 폭발합니다.";
        private const string DescriptionEN = "Centers Blazeshade and Guardian's Staff attacks on the selected cursor position. Blazeshade summons a flame circle for one animation, hitting each enemy once. Chain Ignition spreads in three waves around the aimed position, with one additional explosion at its center during the first wave only.";

        static TargetMakerSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/description.request"))
            {
                File.Delete(Output + "/description.request");
                RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
                if (relic != null)
                {
                    relic.description_KR = DescriptionKR;
                    relic.description_EN = DescriptionEN;
                    EditorUtility.SetDirty(relic);
                    AssetDatabase.SaveAssetIfDirty(relic);
                }
            }
            if (!File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { CreateAssets(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Target Maker/Create Assets")]
        public static void CreateAssets()
        {
            Directory.CreateDirectory(Output);
            Sprite[] attackFrames = Frames("BlazeShadeAttackEffect");
            Sprite[] summonFrames = Frames("BlazeShadeSummonEffect");
            Move("Assets/Nytherion/Art/Combat/Weapons/Animations/Blazeshade/FireShieldLoop.anim", BlazeshadeWeaponSetup.ClipPath);
            AnimationClip loop = Clip(BlazeshadeWeaponSetup.ClipPath, "BlazeShadeAttackLoop", attackFrames, true);
            AnimationClip attack = Clip(AnimationFolder + "BlazeShadeAttackEffect.anim", "BlazeShadeAttackEffect", attackFrames, false);
            AnimationClip summon = Clip(AnimationFolder + "BlazeShadeSummonEffect.anim", "BlazeShadeSummonEffect", summonFrames, false);
            AnimatorController loopController = Controller(BlazeshadeWeaponSetup.ControllerPath, loop);
            GameObject attackPrefab = Effect(AttackPath, attackFrames[0], Controller(AnimationFolder + "BlazeShadeAttackEffect.controller", attack));
            GameObject summonPrefab = Effect(SummonPath, summonFrames[0], Controller(AnimationFolder + "BlazeShadeSummonEffect.controller", summon));

            EditPrefab(BlazeshadeWeaponSetup.AuraPath, root =>
            {
                root.GetComponent<SpriteRenderer>().sprite = attackFrames[0];
                root.GetComponent<Animator>().runtimeAnimatorController = loopController;
            });
            EditPrefab(BlazeshadeWeaponSetup.WeaponPath, root =>
            {
                var serialized = new SerializedObject(root.GetComponent<BlazeshadeWeapon>());
                serialized.FindProperty("summonVisualPrefab").objectReferenceValue = summonPrefab;
                serialized.FindProperty("targetAttackVisualPrefab").objectReferenceValue = attackPrefab;
                serialized.FindProperty("summonClip").objectReferenceValue = summon;
                serialized.FindProperty("targetAttackClip").objectReferenceValue = attack;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            // 기존 GUID와 사용처를 유지하면서 레거시 FireShield 클립과 과속 상태도 함께 교체합니다.
            string legacyClip = "Assets/Nytherion/Art/Skills/Animations/BlazeShadeAttackEffect.anim";
            Move("Assets/Nytherion/Art/Skills/Animations/FireSheild 1.anim", legacyClip);
            Move("Assets/Nytherion/Art/Skills/Animations/FireShield.controller", "Assets/Nytherion/Art/Skills/Animations/BlazeShadeAttackEffect.controller");
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(legacyClip) != null)
                Controller("Assets/Nytherion/Art/Skills/Animations/BlazeShadeAttackEffect.controller",
                    Clip(legacyClip, "BlazeShadeAttackEffect", attackFrames, true));

            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.rarity = Rarity.Rare;
                AssetDatabase.CreateAsset(relic, RelicPath);
            }
            relic.relicName = "TargetMaker";
            relic.koreanName = "타겟 메이커";
            relic.description_KR = DescriptionKR;
            relic.description_EN = DescriptionEN;
            relic.Image = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Relics/Sprites/TargetMaker.png");
            if (relic.Image == null) throw new InvalidOperationException("TargetMaker 아이콘 누락");
            RelicInfluencePolicy.Normalize(relic);
            EditorUtility.SetDirty(relic);
            var database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset");
            if (!database.allRelics.Contains(relic)) database.allRelics.Add(relic);
            EditorUtility.SetDirty(database);
            var pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>("Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Relic/Rare_Relic.asset");
            if (!pool.items.Any(entry => entry.item == relic)) pool.items.Add(new GachaItemRate { item = relic, weight = 100 });
            EditorUtility.SetDirty(pool);
            // 수정한 에셋만 저장해 다른 작업의 미저장 Inspector 값을 보호합니다.
            AssetDatabase.SaveAssetIfDirty(relic);
            AssetDatabase.SaveAssetIfDirty(database);
            AssetDatabase.SaveAssetIfDirty(pool);
            File.WriteAllText(Output + "/setup.txt", $"PASS TargetMaker 아이콘/유물 DB/희귀 뽑기 등록\n공격={attackFrames.Length}프레임, {attack.length:F4}초\n소환={summonFrames.Length}프레임, {summon.length:F4}초\n기본 오라 및 레거시 FireShield 교체 완료\n");
        }

        private static Sprite[] Frames(string name)
        {
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SpriteFolder + name + ".png").OfType<Sprite>()
                .OrderBy(sprite => int.Parse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1))).ToArray();
            if (frames.Length == 0) throw new InvalidOperationException(name + " 프레임 누락");
            return frames;
        }

        private static AnimationClip Clip(string path, string name, Sprite[] frames, bool loop)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.name = name;
            clip.frameRate = 12f;
            clip.ClearCurves();
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.stopTime = frames.Length / clip.frameRate;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }

        private static AnimatorController Controller(string path, AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            var state = machine.AddState(clip.name);
            state.motion = clip;
            state.speed = 1f;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        private static GameObject Effect(string path, Sprite first, AnimatorController controller)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var root = new GameObject(Path.GetFileNameWithoutExtension(path));
                try
                {
                    root.AddComponent<SpriteRenderer>();
                    root.AddComponent<Animator>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            EditPrefab(path, root =>
            {
                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = first;
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                renderer.sortingOrder = 10;
                root.GetComponent<Animator>().runtimeAnimatorController = controller;
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void EditPrefab(string path, Action<GameObject> configure)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Move(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(source) == null) return;
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
                throw new InvalidOperationException("에셋 이름 충돌: " + destination);
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
    }
}
