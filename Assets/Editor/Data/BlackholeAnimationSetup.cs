using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class BlackholeAnimationSetup
    {
        public const string Output = "output/blackhole-animation";
        public const string FieldPath = "Assets/Prefabs/Gameplay/Skills/Blackhole/BlackholeField.prefab";
        public const string SheetPath = "Assets/Nytherion/Art/Skills/Sprites/Blackhole.png";
        public const string Folder = "Assets/Nytherion/Art/Skills/Animations/Blackhole";
        private const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Skill/Blackhole_Skill.asset";

        static BlackholeAnimationSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL: " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Blackhole/애니메이션과 아이콘 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
                .OrderBy(sprite => int.Parse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1))).ToArray();
            if (frames.Length != 16 || frames.Any(sprite => sprite.rect.size != new Vector2(96f, 96f)))
                throw new InvalidOperationException("Blackhole.png의 96×96, 16프레임 슬라이스를 확인해 주세요.");
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Skills/Sprites/Blackhole_Icon.png");
            BlackholeSkillData data = AssetDatabase.LoadAssetAtPath<BlackholeSkillData>(DataPath);
            if (icon == null || data == null) throw new InvalidOperationException("블랙홀 데이터 또는 아이콘이 없습니다.");

            EnsureFolder(Folder);
            AnimationClip summon = CreateClip("BlackholeSummon", frames.Take(4).ToArray(), 12f, false);
            AnimationClip sustainStart = CreateClip("BlackholeSustainStart", frames.Skip(4).Take(3).ToArray(), 8f, false);
            AnimationClip sustain = CreateClip("BlackholeSustain", frames.Skip(7).Take(2).ToArray(), 8f, true);
            AnimationClip despawn = CreateClip("BlackholeDespawn", frames.Skip(9).ToArray(), 12f, false);
            string controllerPath = Folder + "/Blackhole.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState start = ConfigureState(machine, "Summon", summon);
            AnimatorState settle = ConfigureState(machine, "SustainStart", sustainStart);
            AnimatorState loop = ConfigureState(machine, "Sustain", sustain);
            ConfigureState(machine, "Despawn", despawn);
            machine.defaultState = start;
            AddCompletionTransition(start, settle);
            AddCompletionTransition(settle, loop);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject root = PrefabUtility.LoadPrefabContents(FieldPath);
            try
            {
                BlackholeField field = root.GetComponent<BlackholeField>();
                SerializedObject serialized = new SerializedObject(field);
                Transform visual = (Transform)serialized.FindProperty("rangeVisual").objectReferenceValue;
                Transform center = (Transform)serialized.FindProperty("centerVisual").objectReferenceValue;
                if (visual == null || center == null) throw new InvalidOperationException("블랙홀 프리팹의 시각 효과 참조가 없습니다.");
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.enabled = true;
                renderer.color = Color.white;
                visual.localPosition = Vector3.zero;
                float scale = data.range * 2f / (96f / frames[0].pixelsPerUnit);
                visual.localScale = new Vector3(scale, scale, 1f);
                // 새 시트에 중심과 테두리가 함께 포함되어 기존 중심 이미지는 숨깁니다.
                center.gameObject.SetActive(false);
                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.keepAnimatorStateOnDisable = false;
                serialized.FindProperty("visualAnimator").objectReferenceValue = animator;
                serialized.FindProperty("despawnAnimation").objectReferenceValue = despawn;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, FieldPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            data.icon = icon;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/setup.txt",
                "PASS: 소환 1–4 (12fps, 0.333초), 유지 진입 5–7 (8fps, 0.375초 단회), 유지 8–9 (8fps, 0.25초 반복), 소멸 10–16 (12fps, 0.583초).\n" +
                "BlackholeField Animator/소멸 클립 및 Blackhole_Icon 연결. 기존 효과 시간 이후 소멸하고 풀 반환.");
        }

        private static void AddCompletionTransition(AnimatorState source, AnimatorState destination)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.offset = 0f;
        }

        private static AnimatorState ConfigureState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            AnimatorState state = machine.states.FirstOrDefault(child => child.state.name == name).state;
            if (state == null) state = machine.AddState(name);
            foreach (AnimatorStateTransition transition in state.transitions) state.RemoveTransition(transition);
            state.motion = clip;
            state.speed = 1f;
            state.writeDefaultValues = false;
            return state;
        }

        private static AnimationClip CreateClip(string name, Sprite[] frames, float fps, bool loop)
        {
            string path = Folder + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = fps;
            // Sprite 곡선은 마지막 키 뒤에 한 프레임을 포함하므로 끝 키를 중복하지 않습니다.
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f;
            settings.stopTime = frames.Length / fps;
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
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
