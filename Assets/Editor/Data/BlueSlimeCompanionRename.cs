using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Relics;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Nytherion.Editor
{
    /// <summary>푸른 슬라임 소환수의 기존 에셋을 GUID와 저장 ID를 유지하며 이름 변경합니다.</summary>
    [InitializeOnLoad]
    public static class BlueSlimeCompanionRename
    {
        private const string Output = "output/pengoo-rename";
        private const string Prefab = "Assets/Prefabs/Gameplay/Characters/Companions/BlueSlimeCompanion.prefab";
        private const string Relic = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/BlueSlime.asset";
        private const string Animations = "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime";
        private static readonly string[][] Moves =
        {
            new[] { "Assets/Nytherion/Art/Characters/Companions/Drone/Sprites/Frog", "Assets/Nytherion/Art/Characters/Companions/Drone/Sprites/BlueSlime" },
            new[] { "Assets/Nytherion/Art/Skills/Animations/Drone/Frog", "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime" },
            new[] { "Assets/Nytherion/Art/Characters/Companions/Drone/Sprites/BlueSlime/FrogIdle.png", "Assets/Nytherion/Art/Characters/Companions/Drone/Sprites/BlueSlime/BlueSlime_Idle.png" },
            new[] { "Assets/Nytherion/Art/Characters/Companions/Drone/Sprites/BlueSlime/FrogJump.png", "Assets/Nytherion/Art/Characters/Companions/Drone/Sprites/BlueSlime/BlueSlime_Walk.png" },
            new[] { "Assets/Nytherion/Art/Characters/Companions/Drone/Sprites/BlueSlime/FrogCroak.png", "Assets/Nytherion/Art/Characters/Companions/Drone/Sprites/BlueSlime/BlueSlime_Attack.png" },
            new[] { "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/Idle.anim", "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/BlueSlime_Idle.anim" },
            new[] { "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/Jump.anim", "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/BlueSlime_Walk.anim" },
            new[] { "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/Croak.anim", "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/BlueSlime_Attack.anim" },
            new[] { "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/Frog.controller", "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/BlueSlime.controller" },
            new[] { "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/Attack.anim", "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/BlueSlimeLegacyAttackEffect.anim" },
            new[] { "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/AttackEffect.controller", "Assets/Nytherion/Art/Skills/Animations/Drone/BlueSlime/BlueSlimeLegacyAttackEffect.controller" },
            new[] { "Assets/Nytherion/Art/Combat/VFX/Animations/FrogShockwave.anim", "Assets/Nytherion/Art/Combat/VFX/Animations/BlueSlimeShockwave.anim" },
            new[] { "Assets/Nytherion/Art/Combat/VFX/Animations/FrogShockwave.controller", "Assets/Nytherion/Art/Combat/VFX/Animations/BlueSlimeShockwave.controller" },
            new[] { "Assets/Nytherion/Art/Combat/VFX/Sprites/FrogShockwaveSheet.png", "Assets/Nytherion/Art/Combat/VFX/Sprites/Shared/BlueSlimeShockwaveSheet.png" },
            new[] { "Assets/Nytherion/Art/Relics/Sprites/Frog.png", "Assets/Nytherion/Art/Relics/Sprites/SkillRelicS/BlueSlime.png" },
            new[] { "Assets/Prefabs/Gameplay/Characters/Companions/FrogCompanion.prefab", "Assets/Prefabs/Gameplay/Characters/Companions/BlueSlimeCompanion.prefab" },
            new[] { "Assets/Prefabs/Gameplay/Skills/VFX/FrogAttackVFX.prefab", "Assets/Prefabs/Gameplay/Skills/VFX/BlueSlimeAttackVFX.prefab" },
            new[] { "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Frog.asset", "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/BlueSlime.asset" },
            new[] { "Assets/Nytherion/Gameplay/Characters/Companions/FrogCompanion.cs", "Assets/Nytherion/Gameplay/Characters/Companions/BlueSlimeCompanion.cs" },
        };

        static BlueSlimeCompanionRename() => EditorApplication.update += CheckRequest;

        private static void CheckRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/rename.request")) return;
            File.Delete(Output + "/rename.request");
            Rename();
        }

        [MenuItem("Nytherion/Rename Companion to BlueSlime")]
        public static void Rename()
        {
            Directory.CreateDirectory(Output);
            try
            {
                // 전체 대상의 충돌을 먼저 확인합니다. 중간 실패 후 재실행해도 안전합니다.
                foreach (string[] move in Moves)
                {
                    if (Exists(move[0]) && Exists(move[1]))
                        throw new InvalidOperationException("이름 변경 대상이 이미 존재합니다: " + move[1]);
                }
                foreach (string[] move in Moves)
                {
                    if (move[0].EndsWith(".cs")) continue;
                    if (!Exists(move[0]))
                    {
                        if (Exists(move[1])) continue;
                        throw new InvalidOperationException("이름 변경할 에셋이 없습니다: " + move[0]);
                    }
                    string guid = AssetDatabase.AssetPathToGUID(move[0]);
                    string error = AssetDatabase.MoveAsset(move[0], move[1]);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                    if (guid != AssetDatabase.AssetPathToGUID(move[1]))
                        throw new InvalidOperationException("에셋 GUID가 변경되었습니다: " + move[1]);
                }

                RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(Relic);
                if (relic == null || relic.relicName != "Frog")
                    throw new InvalidOperationException("기존 세이브 ID Frog를 확인할 수 없습니다.");
                relic.name = "BlueSlime";
                relic.koreanName = "푸른 슬라임 표본";
                relic.englishName = "Blue Slime Specimen";
                relic.description_KR = "푸른 슬라임을 소환합니다.";
                relic.description_EN = "Summons a blue slime.";
                EditorUtility.SetDirty(relic);

                RenamePrefab(Prefab, "BlueSlimeCompanion", true);
                RenamePrefab("Assets/Prefabs/Gameplay/Skills/VFX/BlueSlimeAttackVFX.prefab", "BlueSlimeAttackVFX", false);
                foreach (string[] move in Moves)
                {
                    string path = move[1];
                    if (!path.EndsWith(".anim") && !path.EndsWith(".controller")) continue;
                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    asset.name = Path.GetFileNameWithoutExtension(path);
                    EditorUtility.SetDirty(asset);
                }
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "/BlueSlime.controller");
                AnimatorControllerParameter[] parameters = controller.parameters;
                foreach (AnimatorControllerParameter parameter in parameters)
                    if (parameter.name == "Croaking") parameter.name = "Attack";
                controller.parameters = parameters;
                foreach (AnimatorStateTransition transition in controller.layers[0].stateMachine.anyStateTransitions)
                {
                    AnimatorCondition[] conditions = transition.conditions;
                    for (int i = 0; i < conditions.Length; i++)
                        if (conditions[i].parameter == "Croaking") conditions[i].parameter = "Attack";
                    transition.conditions = conditions;
                    EditorUtility.SetDirty(transition);
                }
                foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
                    if (child.state.name == "Croak") { child.state.name = "Attack"; EditorUtility.SetDirty(child.state); }
                EditorUtility.SetDirty(controller);

                var tables = LocalizationEditorSettings.GetStringTableCollection("Relics");
                if (tables != null)
                {
                    foreach (StringTable table in tables.StringTables)
                    {
                        bool korean = table.LocaleIdentifier.Code.StartsWith("ko", StringComparison.OrdinalIgnoreCase);
                        bool english = table.LocaleIdentifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
                        if (!korean && !english) continue;
                        table.AddEntry("relic.Frog.name", korean ? relic.koreanName : relic.EnglishName);
                        table.AddEntry("relic.Frog.description", korean ? relic.description_KR : relic.description_EN);
                        EditorUtility.SetDirty(table);
                    }
                    EditorUtility.SetDirty(tables.SharedData);
                }

                string oldScriptPath = "Assets/Nytherion/Gameplay/Characters/Companions/FrogCompanion.cs";
                string scriptPath = "Assets/Nytherion/Gameplay/Characters/Companions/BlueSlimeCompanion.cs";
                if (Exists(oldScriptPath))
                {
                    string error = AssetDatabase.MoveAsset(oldScriptPath, scriptPath);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                }
                string code = File.ReadAllText(scriptPath);
                code = code.Replace("FrogCompanion", "BlueSlimeCompanion")
                    .Replace("Croak 애니메이션", "Attack 애니메이션").Replace("개구리 소환수", "푸른 슬라임 소환수");
                File.WriteAllText(scriptPath, code);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                File.WriteAllText(Output + "/result.txt", "PASS: BlueSlime rename applied; GUIDs and Frog save ID preserved.");
                Debug.Log("[BlueSlimeCompanionRename] 푸른 슬라임 이름과 에셋 경로를 변경했습니다. 저장 ID와 GUID는 유지합니다.");
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/result.txt", "FAIL: " + error);
                Debug.LogException(error);
            }
        }

        private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

        private static void RenamePrefab(string path, string name, bool companion)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.name = name;
                if (companion)
                {
                    // 기존 이동·전투 설정은 보존하고 애니메이션 파라미터만 변경합니다.
                    foreach (MonoBehaviour behaviour in contents.GetComponents<MonoBehaviour>())
                    {
                        if (behaviour == null) continue;
                        SerializedObject settings = new SerializedObject(behaviour);
                        SerializedProperty trigger = settings.FindProperty("attackTriggerParam");
                        if (trigger == null) continue;
                        trigger.stringValue = "Attack";
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }
}
