using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Gameplay.Relics.Modules;
using Nytherion.UI.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nytherion.Editor
{
    /// <summary>스킬 직접 보상을 해당 스킬 각인의 해금 보상으로 전환합니다.</summary>
    [InitializeOnLoad]
    public static class SkillRelicMilestoneSetup
    {
        private const string Output = "output/skill-relic-milestones";
        private const string DataRoot = "Assets/Nytherion/Data/ScriptableObjects/";
        private const string PlayPending = "Nytherion.SkillRelicMilestones.VerifyPlay";
        private static double playReadyAt;

        static SkillRelicMilestoneSetup()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayPending, false))
                    playReadyAt = EditorApplication.timeSinceStartup + 8d;
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/verify-play.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(Output + "/verify-play.request");
                SessionState.SetBool(PlayPending, true);
                EditorApplication.isPlaying = true;
                return;
            }
            if (SessionState.GetBool(PlayPending, false) && EditorApplication.isPlaying &&
                playReadyAt > 0d && EditorApplication.timeSinceStartup >= playReadyAt)
            {
                var results = new List<string>();
                try
                {
                    GachaManager gacha = UnityEngine.Object.FindObjectOfType<GachaManager>();
                    ProgressionManager progression = UnityEngine.Object.FindObjectOfType<ProgressionManager>();
                    Require(gacha != null && progression != null && progression.IsInitialized,
                        "GameScene 플레이 모드: 가챠·진척도 매니저 초기화", results);
                    Require(ReferenceEquals(typeof(GachaManager).GetField("progressionManager", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(gacha), progression), "실제 씬 IProgressionManager DI 연결", results);
                    Require(progression.GetAllMilestones().Any(m => m != null && m.rewards != null &&
                        m.rewards.Any(r => r != null && r.rewardType == RewardType.RelicUnlock)), "실제 씬 해금 업적 데이터 연결", results);
                }
                catch (Exception error) { results.Add("FAIL " + error); }
                finally
                {
                    File.WriteAllLines(Output + "/play-verification.txt", results);
                    SessionState.SetBool(PlayPending, false);
                    EditorApplication.isPlaying = false;
                }
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/apply.request")) return;
            File.Delete(Output + "/apply.request");
            Apply();
        }

        [MenuItem("Tools/Nytherion/Progression/Convert Skill Rewards To Relic Unlocks")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            try
            {
                foreach (MilestoneData milestone in Find<MilestoneData>())
                {
                    if (milestone.rewards == null) continue;
                    bool changed = false;
                    foreach (RewardData reward in milestone.rewards)
                    {
                        if (reward != null && reward.rewardType == RewardType.RelicUnlock && reward.relicData != null)
                            EnsureRelicIcon(reward.relicData, milestone.icon);
                        if (reward == null || reward.rewardType != RewardType.Skill) continue;
                        if (reward.skillData == null || !reward.skillData.IsRuntimeAvailable)
                        {
                            results.Add("SKIP 기존 스킬 참조 없음 또는 비활성: " + milestone.name);
                            continue;
                        }

                        RelicData relic = GetOrCreateSkillRelic(reward.skillData);
                        Undo.RecordObject(milestone, "Convert Skill Milestone Reward");
                        reward.rewardType = RewardType.RelicUnlock;
                        reward.relicData = relic;
                        reward.skillData = null;
                        reward.amount = 1;
                        EnsureRelicIcon(relic, milestone.icon);
                        changed = true;
                        results.Add("CONVERT " + milestone.milestoneID + " -> " + relic.name);
                    }
                    if (changed) EditorUtility.SetDirty(milestone);
                }
                AssetDatabase.SaveAssets();
                Verify(results);
                results.Add("PASS Unity Editor 컴파일 및 실제 에셋 검증 완료");
            }
            catch (Exception error)
            {
                results.Add("FAIL " + error);
                Debug.LogException(error);
            }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        public static RelicData GetOrCreateSkillRelic(SkillData skill)
        {
            RelicData relic = Find<RelicData>().FirstOrDefault(data => data.IsRuntimeAvailable &&
                data.effectModules != null && data.effectModules.Any(module => module != null &&
                    module.effects != null && module.effects.Any(effect => effect is GrantSkillEffect grant &&
                        grant.skillData == skill)));
            if (relic != null) return relic;

            relic = ScriptableObject.CreateInstance<RelicData>();
            relic.relicName = "Relic of " + skill.skillID;
            relic.koreanName = skill.skillName + " 각인";
            relic.Image = skill.icon;
            RelicDescriptionSetup.GetSkillDescriptions(skill, out relic.description_KR, out relic.description_EN);
            relic.rarity = Rarity.Rare;
            relic.effectModules = new List<RelicEffectModule>
            {
                new RelicEffectModule { effects = new List<RelicEffectBase> { new GrantSkillEffect { skillData = skill } } }
            };
            string path = AssetDatabase.GenerateUniqueAssetPath(DataRoot + "Relics/SkillRelics/Relic_" + skill.skillID + ".asset");
            AssetDatabase.CreateAsset(relic, path);
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(DataRoot + "Relics/RelicDatabase.asset");
            if (database == null) throw new InvalidOperationException("유물 데이터베이스 참조가 없습니다.");
            Undo.RecordObject(database, "Register Skill Relic");
            if (database.allRelics == null) database.allRelics = new List<RelicData>();
            database.allRelics.Add(relic);
            EditorUtility.SetDirty(database);
            return relic;
        }

        private static void EnsureRelicIcon(RelicData relic, Sprite previousIcon)
        {
            if (relic.Image != null) return;
            Sprite icon = previousIcon;
            foreach (RelicEffectModule module in relic.effectModules ?? new List<RelicEffectModule>())
            {
                if (module == null || module.effects == null) continue;
                foreach (RelicEffectBase effect in module.effects)
                {
                    if (!(effect is GrantSkillEffect grant) || grant.skillData == null) continue;
                    if (icon == null) icon = grant.skillData.icon;
                    if (icon == null && grant.skillData.skillPrefab != null)
                        icon = grant.skillData.skillPrefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite;
                }
            }
            if (icon == null) return;
            Undo.RecordObject(relic, "Restore Skill Relic Icon");
            relic.Image = icon;
            EditorUtility.SetDirty(relic);
        }

        private static void Verify(List<string> results)
        {
            MilestoneDatabaseSO database = AssetDatabase.LoadAssetAtPath<MilestoneDatabaseSO>(DataRoot + "Progression/MilestoneDatabase.asset");
            var milestones = database.allMilestones.Where(m => m != null).ToList();
            Require(milestones.All(m => m.rewards == null || m.rewards.All(r => r == null ||
                r.rewardType != RewardType.Skill || r.skillData == null || !r.skillData.IsRuntimeAvailable)), "활성 스킬 직접 보상 제거", results);
            var unlocks = milestones.Where(m => m.rewards != null && m.rewards.Any(r =>
                r != null && r.rewardType == RewardType.RelicUnlock)).ToList();
            foreach (MilestoneData milestone in unlocks)
                foreach (RewardData reward in milestone.rewards.Where(r => r != null && r.rewardType == RewardType.RelicUnlock))
                    Require(reward.relicData != null && reward.relicData.Image != null,
                        "유물 아이콘 참조: " + milestone.milestoneID, results);
            Require(unlocks.Count > 0 && unlocks.All(m => m.rewards.Where(r => r != null &&
                r.rewardType == RewardType.RelicUnlock).All(r => r.relicData != null &&
                    r.relicData.IsRuntimeAvailable && r.relicData.Image != null && m.DisplayIcon == r.relicData.Image)),
                "등록된 해금 업적의 유물·아이콘 참조 (" + unlocks.Count + "개)", results);

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Skill Relic Milestone Verification");
                SceneManager.MoveGameObjectToScene(root, preview);
                ProgressionManager manager = root.AddComponent<ProgressionManager>();
                Set(manager, "milestoneDatabase", database);
                manager.Construct(null, null);
                GachaManager gacha = root.AddComponent<GachaManager>();
                gacha.Construct(null, null, null, manager);
                MethodInfo check = typeof(GachaManager).GetMethod("IsRuntimeAvailable", BindingFlags.Instance | BindingFlags.NonPublic);
                Func<ScriptableObject, bool> canDraw = item => (bool)check.Invoke(gacha, new object[] { item });
                var relics = unlocks.SelectMany(m => m.rewards).Where(r => r != null && r.rewardType == RewardType.RelicUnlock)
                    .Select(r => r.relicData).Distinct().ToList();
                Require(relics.All(r => !manager.IsRelicUnlocked(r) && !canDraw(r)), "새 저장: 업적 미완료 각인 뽑기 차단", results);

                int skillsGranted = 0;
                manager.OnSkillUnlocked += _ => skillsGranted++;
                foreach (MilestoneData milestone in unlocks)
                {
                    manager.LoadFromSaveData(new SaveData());
                    foreach (MilestoneData prerequisite in milestone.requiredMilestones ?? Array.Empty<MilestoneData>())
                        manager.CompleteMilestone(prerequisite.milestoneID);
                    manager.AddProgress(milestone, milestone.targetValue);
                    Require(manager.IsMilestoneCompleted(milestone.milestoneID) && milestone.rewards
                        .Where(r => r != null && r.rewardType == RewardType.RelicUnlock)
                        .All(r => manager.IsRelicUnlocked(r.relicData) && canDraw(r.relicData)),
                        "업적 달성 즉시 각인 해금: " + milestone.milestoneID, results);
                }
                Require(skillsGranted == 0, "업적 달성으로 스킬 직접 부여 없음", results);

                var legacy = new SaveData();
                legacy.progressionState.completedMilestones = unlocks.Select(m => m.milestoneID).ToList();
                manager.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(legacy)));
                Require(relics.All(manager.IsRelicUnlocked), "기존 완료 업적 저장 파일: 재달성 없이 해금", results);
                var saved = new SaveData();
                manager.PopulateSaveData(saved);
                manager.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)));
                Require(relics.All(manager.IsRelicUnlocked), "저장·재로드 해금 유지", results);

                var imageObject = new GameObject("Achievement Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imageObject.transform.SetParent(root.transform);
                Image image = imageObject.GetComponent<Image>();
                MilestoneSlotUI slot = imageObject.AddComponent<MilestoneSlotUI>();
                Set(slot, "iconImage", image);
                foreach (MilestoneData milestone in unlocks)
                {
                    slot.Initialize(milestone);
                    Require(image.sprite == milestone.DisplayIcon && image.gameObject.activeSelf, "업적 슬롯 아이콘: " + milestone.milestoneID, results);
                }

                foreach (GachaPoolSO pool in Find<GachaPoolSO>().Where(pool => pool.items != null))
                {
                    manager.LoadFromSaveData(new SaveData());
                    for (int i = 0; i < 100; i++)
                    {
                        ScriptableObject item = pool.GetRandomItem(canDraw);
                        if (item is RelicData relic) Require(!relics.Contains(relic), "미해금 뽑기 제외", null);
                    }
                }
                results.Add("PASS 실제 가챠 풀 반복 추첨: 미해금 각인 제외");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static IEnumerable<T> Find<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset != null);

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Require(bool condition, string message, List<string> results)
        {
            if (!condition) throw new InvalidOperationException(message);
            results?.Add("PASS " + message);
        }
    }
}
