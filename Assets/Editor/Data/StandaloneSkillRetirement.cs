using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>폐기한 독립 스킬의 참조를 정리하고 전용 에셋을 Unity API로 삭제합니다.</summary>
    [InitializeOnLoad]
    public static class StandaloneSkillRetirement
    {
        private const string Output = "output/standalone-skill-retirement";
        private const string SkillDirectory = "Assets/Nytherion/Data/ScriptableObjects/Skill/";
        private const string RelicDirectory = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/";
        private static readonly string[] RetiredNames = { "Atk_Buff_Skill", "AS_Skill", "Dash_CD_Skill", "Laser_Skill", "Aura_Skill" };
        private static readonly string[] RetiredRelics = { "BearTotem", "BlueOre", "MysticalFeather", "Relic_Laser", "Relic_Aura" };
        private static readonly string[] RetiredIds = { "skill_Atk_buff", "skill_attack_speed", "skill_Dash_cooldown", "skill_laser", "skill_aura" };
        private static readonly string[] RetiredPrefabPaths =
        {
            "Assets/Prefabs/Gameplay/Skills/Buffs/AtkBuffSkillPrefab.prefab",
            "Assets/Prefabs/Gameplay/Skills/Buffs/AttackSpeedBoost.prefab",
            "Assets/Prefabs/Gameplay/Skills/Buffs/CoolDownSkillPrefab.prefab",
            "Assets/Prefabs/Gameplay/Skills/LaserSkill.prefab",
            "Assets/Prefabs/Gameplay/Skills/AuraSkilPrefab.prefab"
        };
        private static readonly string[] RetiredCodePaths =
        {
            "Assets/Nytherion/Gameplay/Skills/Abilities/Laser/LaserSkill.cs",
            "Assets/Nytherion/Data/ScriptableObjects/Skill/LaserSkillData.cs",
            "Assets/Nytherion/Gameplay/Skills/Abilities/Aura/AuraSkill.cs",
            "Assets/Nytherion/Data/ScriptableObjects/Skill/AuraSkillData.cs"
        };

        private static IEnumerable<string> RemovedPaths => RetiredNames.Select(name => SkillDirectory + name + ".asset")
            .Concat(RetiredRelics.Select(name => RelicDirectory + name + ".asset"))
            .Concat(RetiredPrefabPaths).Concat(RetiredCodePaths)
            .Concat(new[] { "Assets/Nytherion/Data/ScriptableObjects/Progression/Aura_Milestone.asset" });

        static StandaloneSkillRetirement() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/delete.request")) return;
            File.Delete(Output + "/delete.request");
            Apply();
        }

        [MenuItem("Tools/Nytherion/Skills/Retire Standalone Buffs, Laser And Aura")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            try
            {
                // 에셋 저장 후 자동 동기화가 삭제 대상의 각인과 스킬을 다시 등록하지 않도록 먼저 차단합니다.
                foreach (SkillData skill in Find<SkillData>())
                {
                    if (!RetiredIds.Contains(skill.skillID) || skill.isDisabled) continue;
                    skill.isDisabled = true;
                    Save(skill);
                }
                foreach (RelicData relic in Find<RelicData>())
                {
                    if (!RetiredRelics.Contains(relic.name) || relic.isDisabled) continue;
                    relic.isDisabled = true;
                    Save(relic);
                }
                foreach (SkillDatabaseSO database in Find<SkillDatabaseSO>())
                {
                    if (database.allSkills == null) continue;
                    if (database.allSkills.RemoveAll(skill => skill != null && RetiredIds.Contains(skill.skillID)) > 0) Save(database);
                }
                foreach (RelicDatabaseSO database in Find<RelicDatabaseSO>())
                {
                    if (database.allRelics != null && database.allRelics.RemoveAll(relic =>
                        relic != null && RetiredRelics.Contains(relic.name)) > 0) Save(database);
                }
                foreach (GachaPoolSO pool in Find<GachaPoolSO>())
                {
                    if (pool.items == null) continue;
                    if (pool.items.RemoveAll(rate => rate != null &&
                        ((rate.item is SkillData skill && RetiredIds.Contains(skill.skillID)) ||
                         (rate.item is RelicData relic && RetiredRelics.Contains(relic.name)))) > 0) Save(pool);
                }

                var obsoleteMilestones = new HashSet<MilestoneData>();
                foreach (MilestoneData milestone in Find<MilestoneData>())
                {
                    if (milestone.name == "Aura_Milestone") obsoleteMilestones.Add(milestone);
                    if (milestone.rewards == null) continue;
                    int removed = milestone.rewards.RemoveAll(reward => reward != null &&
                        ((reward.skillData != null && RetiredIds.Contains(reward.skillData.skillID)) ||
                         (reward.relicData != null && RetiredRelics.Contains(reward.relicData.name))));
                    if (removed == 0) continue;
                    Save(milestone);
                    if (milestone.rewards.Count == 0) obsoleteMilestones.Add(milestone);
                }
                foreach (MilestoneDatabaseSO database in Find<MilestoneDatabaseSO>())
                {
                    if (database.allMilestones != null && database.allMilestones.RemoveAll(obsoleteMilestones.Contains) > 0) Save(database);
                }
                DeleteExclusiveAssets(results);
                Verify(results);
                results.Add("PASS Unity Editor 컴파일 및 에셋 등록·지급·기존 세이브 왕복 검증 완료");
            }
            catch (Exception error)
            {
                results.Add("FAIL " + error);
                Debug.LogException(error);
            }
            finally
            {
                File.WriteAllLines(Output + "/deletion-verification.txt", results);
            }
        }

        private static void DeleteExclusiveAssets(List<string> results)
        {
            var paths = new HashSet<string>(RemovedPaths);
            string[] guids = paths.Where(File.Exists).Select(path => AssetDatabase.AssetPathToGUID(path))
                .Where(guid => !string.IsNullOrEmpty(guid)).ToArray();
            // 다른 기능의 참조가 남아 있으면 삭제 전에 중단하여 누락 참조를 만들지 않습니다.
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || paths.Contains(path) || !File.Exists(path)) continue;
                string extension = Path.GetExtension(path);
                if (!new[] { ".asset", ".prefab", ".unity", ".anim", ".controller", ".mat" }.Contains(extension)) continue;
                string content = File.ReadAllText(path);
                if (guids.Any(content.Contains)) throw new InvalidOperationException("삭제 대상의 외부 참조가 남아 있습니다: " + path);
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    if (!File.Exists(path)) continue;
                    if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("에셋 삭제 실패: " + path);
                    results.Add("DELETE " + path);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }


        private static void Verify(List<string> results)
        {
            SkillDatabaseSO database = Load<SkillDatabaseSO>(SkillDirectory + "SkillDatabaseSO.asset");
            Require(RemovedPaths.All(path => !File.Exists(path) && !File.Exists(path + ".meta")), $"전용 에셋·코드 {RemovedPaths.Count()}개와 meta 실제 삭제", results);
            var retired = RetiredIds.Select(id =>
            {
                SkillData data = ScriptableObject.CreateInstance<SkillData>();
                data.skillID = id;
                data.isDisabled = true;
                return data;
            }).ToArray();
            Require(retired.All(skill => !skill.IsRuntimeAvailable && database.GetSkillById(skill.skillID) == null), $"폐기 스킬 {RetiredIds.Length}개 조회 차단", results);
            Require(database.GetSkillByType(SkillType.Aura) == null, "오라 스킬 타입 조회 차단", results);
            Require(Find<GachaPoolSO>().All(pool => pool.items == null || pool.items.All(rate => rate == null ||
                !(rate.item is SkillData skill && RetiredIds.Contains(skill.skillID)) &&
                !(rate.item is RelicData relic && RetiredRelics.Contains(relic.name)))), "스킬·유물 가챠 제외", results);
            Require(Find<RelicDatabaseSO>().All(db => db.allRelics == null || db.allRelics.All(relic =>
                relic == null || !RetiredRelics.Contains(relic.name))), "대응 각인 DB 참조 제거", results);
            Require(Find<MilestoneDatabaseSO>().All(db => db.allMilestones == null || db.allMilestones.All(m => m == null ||
                m.rewards == null || m.rewards.All(reward => reward == null ||
                    (reward.skillData == null || !RetiredIds.Contains(reward.skillData.skillID)) &&
                    (reward.relicData == null || !RetiredRelics.Contains(reward.relicData.name))))), "진척도 보상 제외", results);

            StatBuffSkillData overdrive = Load<StatBuffSkillData>(SkillDirectory + "Overdrive_Skill.asset");
            Require(overdrive.IsRuntimeAvailable && overdrive.skillPrefab.GetComponent<StatBuffSkill>() != null &&
                overdrive.statModifiers.Count == 5 && overdrive.statModifiers.All(modifier => modifier.stat != StatType.DashCooldown),
                "오버드라이브 공격력·공속·대시 속도 통합, 대시 쿨타임 제외", results);

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("폐기 스킬 저장 호환성 검증");
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, preview);
                SkillDataManager manager = root.AddComponent<SkillDataManager>();
                manager.skillDatabase = database;
                foreach (SkillData skill in retired)
                {
                    manager.AcquireSkill(skill);
                    manager.GrantSkillFromRelic(skill, 5);
                }
                Require(manager.skillStates.Count == 0 && manager.storageSkills.All(skill => skill == null), "직접 획득·각인 지급 차단", results);

                // 저장된 보관함 순서와 장착 슬롯의 빈자리를 유지합니다.
                var old = new SaveData
                {
                    ownedSkills = new List<SkillEntry>
                    {
                        new SkillEntry { skillId = retired[0].skillID, level = 4, exp = 2 },
                        new SkillEntry { skillId = overdrive.skillID, level = 3, exp = 1 },
                        new SkillEntry { skillId = retired[1].skillID, level = 2 },
                        new SkillEntry { skillId = retired[2].skillID, level = 2 },
                        new SkillEntry { skillId = retired[3].skillID, level = 2 },
                        new SkillEntry { skillId = retired[4].skillID, level = 5, exp = 3 }
                    },
                    equippedSkillIds = new List<string> { retired[4].skillID, retired[2].skillID, retired[3].skillID }
                };
                manager.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(old)));
                Require(manager.storageSkills[0] == null && manager.storageSkills[1] == overdrive &&
                    manager.storageSkills[5] == null &&
                    manager.equippedSkills.All(skill => skill == null) && manager.skillStates.Count == 1 &&
                    manager.skillStates[overdrive.skillID].level == 3 && manager.skillStates[overdrive.skillID].exp == 1,
                    "기존 세이브 로드: 폐기 스킬 제거·오버드라이브 레벨·경험치·슬롯 보존", results);
                var saved = new SaveData();
                manager.PopulateSaveData(saved);
                manager.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)));
                Require(manager.storageSkills[1] == overdrive && manager.skillStates.Count == 1 &&
                    saved.ownedSkills.All(entry => !retired.Any(skill => skill.skillID == entry.skillId)), "저장·재로드 왕복", results);

                VerifyBuff(root, manager, overdrive, results);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                foreach (SkillData skill in retired) Object.DestroyImmediate(skill);
            }
        }

        private static void VerifyBuff(GameObject root, SkillDataManager states, StatBuffSkillData data, List<string> results)
        {
            PlayerManager player = root.AddComponent<PlayerManager>();
            PlayerData baseline = ScriptableObject.CreateInstance<PlayerData>();
            baseline.rangedDamage = 100f;
            baseline.rangedSpeed = 1f;
            baseline.dashSpeed = 5f;
            baseline.dashCooldown = 3f;
            typeof(PlayerManager).GetField("basePlayerData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, baseline);
            player.Construct(null, null, null, null, states);
            StatBuffSkill skill = root.AddComponent<StatBuffSkill>();
            skill.skillData = data;
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(StatBuffSkill).GetField("playerManager", flags).SetValue(skill, player);
            typeof(StatBuffSkill).GetField("statBuffData", flags).SetValue(skill, data);
            try
            {
                var routine = (IEnumerator)typeof(StatBuffSkill).GetMethod("BuffRoutine", flags).Invoke(skill, null);
                Require(routine.MoveNext(), "오버드라이브 버프 시작", results);
                Require(Mathf.Approximately(player.currentPlayerData.rangedDamage, 140f) &&
                    Mathf.Approximately(player.currentPlayerData.rangedSpeed, 1.4f) &&
                    Mathf.Approximately(player.currentPlayerData.dashSpeed, 7f) &&
                    Mathf.Approximately(player.currentPlayerData.dashCooldown, 3f),
                    "저장된 3레벨 적용: 공격력·공속·대시 속도 +40%, 대시 쿨타임 유지", results);
                Require(!routine.MoveNext() && Mathf.Approximately(player.currentPlayerData.rangedDamage, 100f) &&
                    Mathf.Approximately(player.currentPlayerData.rangedSpeed, 1f) && Mathf.Approximately(player.currentPlayerData.dashSpeed, 5f),
                    "지속 종료 후 버프 해제", results);
                routine = (IEnumerator)typeof(StatBuffSkill).GetMethod("BuffRoutine", flags).Invoke(skill, null);
                routine.MoveNext();
                // 비활성 미리보기 객체에는 Unity가 OnDestroy를 보내지 않으므로 해제 콜백을 직접 검증합니다.
                typeof(StatBuffSkill).GetMethod("OnDestroy", flags).Invoke(skill, null);
                Object.DestroyImmediate(skill);
                Require(Mathf.Approximately(player.currentPlayerData.rangedDamage, 100f) &&
                    Mathf.Approximately(player.currentPlayerData.rangedSpeed, 1f), "장착 해제·파괴 시 버프 잔류 없음", results);
            }
            finally
            {
                if (skill != null) Object.DestroyImmediate(skill);
                if (player.currentPlayerData != null) Object.DestroyImmediate(player.currentPlayerData);
                Object.DestroyImmediate(baseline);
            }
        }

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("에셋 로드 실패: " + path);

        private static IEnumerable<T> Find<T>() where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset != null);

        private static void Save(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static void Require(bool passed, string message, List<string> results)
        {
            if (!passed) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
        }
    }
}
