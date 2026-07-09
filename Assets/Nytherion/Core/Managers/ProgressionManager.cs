using System;
using UnityEngine;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Relics;
using VContainer;
using System.Collections.Generic;
using Nytherion.GamePlay.Relics;

namespace Nytherion.Core.Managers
{
    public interface IProgressionManager
    {
        bool IsSkillUnlocked(SkillData skillData);
        bool IsSkillUnlocked(string skillId);
        void UnlockSkill(SkillData skillData);
        void LockSkill(SkillData skillData);
        void LockSkill(string skillId);
        bool IsMilestoneCompleted(string milestoneId);
        void CompleteMilestone(string milestoneId);
        void CompleteMilestone(MilestoneData milestone);
        bool IsMilestoneAvailable(MilestoneData milestone);

        int GetCurrentProgress(string milestoneId);
        void AddProgress(MilestoneData milestone, int amount = 1);
        void ProcessAction(ProgressionType type, int amount = 1);

        event Action<string, int, int> OnMilestoneProgressUpdated;

        event Action<SkillData> OnSkillUnlocked;
        event Action<SkillData> OnSkillLocked;
        event Action<string> OnMilestoneCompleted;

        void RecordProjectile(GameObject projectilePrefab);
        System.Collections.Generic.List<GameObject> GetUnlockedProjectilePrefabs();
        void RecordProjectile(string projectileTag);
        System.Collections.Generic.List<string> GetUnlockedProjectiles();
        System.Collections.Generic.List<MilestoneData> GetAllMilestones();
    }

    public class ProgressionManager : BaseManager, IProgressionManager, ISaveable 
    {
        [Header("Databases")]
        [SerializeField] private MilestoneDatabaseSO milestoneDatabase;

        private ProgressionState state = new ProgressionState();
        private System.Collections.Generic.List<GameObject> unlockedProjectilePrefabs = new System.Collections.Generic.List<GameObject>();

        private System.Collections.Generic.Dictionary<string, MilestoneData> milestoneLookup = new System.Collections.Generic.Dictionary<string, MilestoneData>();
        private System.Collections.Generic.Dictionary<ProgressionType, System.Collections.Generic.List<MilestoneData>> milestonesByType = new System.Collections.Generic.Dictionary<ProgressionType, System.Collections.Generic.List<MilestoneData>>();
        private System.Collections.Generic.Dictionary<string, int> skillUnlockRefCount = new System.Collections.Generic.Dictionary<string, int>();

        private CurrencyDataManager currencyDataManager;
        private InventoryDataManager inventoryDataManager;
        private RelicManager relicManager;
        private EventManager eventManager;
        private IObjectResolver container;

        [Inject]
        public void Construct(IObjectResolver container, RelicManager relicManager)
        {
            this.container = container;
            this.relicManager = relicManager;

            InitializeMilestoneLookup();
        }

        protected override void OnInitializeInternal()
        {
            if (container != null)
            {
                currencyDataManager = container.Resolve<CurrencyDataManager>();
                inventoryDataManager = container.Resolve<InventoryDataManager>();
            }
            
            if (RootLifetimeScope.Instance != null && RootLifetimeScope.Instance.Container != null)
            {
                eventManager = RootLifetimeScope.Instance.Container.Resolve<EventManager>();
            }
            else
            {
                eventManager = FindObjectOfType<EventManager>();
            }

            SubscribeToEvents();
        }

        private float playTimeAccumulator = 0f;

        private void Update()
        {
            if (!IsInitialized) return;

            playTimeAccumulator += Time.deltaTime;
            if (playTimeAccumulator >= 1.0f)
            {
                int seconds = (int)playTimeAccumulator;
                ProcessAction(ProgressionType.TotalPlayTime, seconds);
                playTimeAccumulator -= seconds;
            }
        }

        private void SubscribeToEvents()
        {
            if (eventManager == null) return;

            eventManager.OnEnemyDied += (enemy) => ProcessAction(ProgressionType.KillEnemy, 1);
            eventManager.OnEnemyDamagedByPlayer += (damage) => ProcessAction(ProgressionType.DealDamage, (int)damage);
            eventManager.OnBossClearedEvent += (stage) => {
                ProcessAction(ProgressionType.ClearFloor, 1);

                // 구석 조약돌 (CornerStone) 가장자리 배치 상태로 보스 클리어 업적 연동
                if (relicManager != null)
                {
                    bool isCornerStoneMet = false;
                    foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                    {
                        RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                        if (block != null && block.RelicId == "CornerStone" && !block.SourceData.isDisabled)
                        {
                            int r = pair.Value.y;
                            int c = pair.Value.x;
                            if (r == 0 || r == relicManager.GridRows - 1 || c == 0 || c == relicManager.GridColumns - 1)
                            {
                                isCornerStoneMet = true;
                                break;
                            }
                        }
                    }

                    if (isCornerStoneMet)
                    {
                        ProcessAction(ProgressionType.ComfyCornerClear, 1);
                    }
                }

                // 유리 칼 (Glass Sword) 장착 상태로 보스 클리어 업적 연동
                if (relicManager != null)
                {
                    bool isGlassCapEquipped = false;
                    foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                    {
                        RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                        if (block != null && block.RelicId == "Glass Sword" && !block.SourceData.isDisabled)
                        {
                            isGlassCapEquipped = true;
                            break;
                        }
                    }

                    if (isGlassCapEquipped)
                    {
                        ProcessAction(ProgressionType.GlassCapBossClear, 1);
                    }
                }
            };

            if (currencyDataManager != null)
            {
                currencyDataManager.OnDataChanged += HandleCurrencyChanged;
            }
        }

        private void HandleCurrencyChanged(CurrencyChangeData data)
        {
            // 로딩 중이거나 지출/설정(변화량 0이하)인 경우는 진척도에 반영하지 않음
            if (data.isSilent || data.changeAmount <= 0) return;

            if (data.currencyType == CurrencyType.Gold)
            {
                ProcessAction(ProgressionType.CollectGold, data.changeAmount);
            }
            else if (data.currencyType == CurrencyType.Token)
            {
                ProcessAction(ProgressionType.EarnToken, data.changeAmount);
            }
        }

        private void InitializeMilestoneLookup()
        {
            if (milestoneDatabase == null)
            {
                Debug.LogError("[ProgressionManager] milestoneDatabase가 Null입니다! 프리팹 또는 인스펙터 바인딩을 확인하세요.");
                return;
            }
            if (milestoneDatabase.allMilestones == null)
            {
                Debug.LogError("[ProgressionManager] milestoneDatabase.allMilestones가 Null입니다!");
                return;
            }

            Debug.Log($"[ProgressionManager] InitializeMilestoneLookup 성공. 마일스톤 개수: {milestoneDatabase.allMilestones.Count}");

            milestoneLookup.Clear();
            milestonesByType.Clear();

            foreach (MilestoneData milestone in milestoneDatabase.allMilestones)
            {
                if (milestone == null) continue;

                if (!milestoneLookup.ContainsKey(milestone.milestoneID))
                {
                    milestoneLookup.Add(milestone.milestoneID, milestone);
                }

                if (!milestonesByType.ContainsKey(milestone.progressionType))
                {
                    milestonesByType.Add(milestone.progressionType, new System.Collections.Generic.List<MilestoneData>());
                }
                milestonesByType[milestone.progressionType].Add(milestone);
            }
        }

        public event Action<SkillData> OnSkillUnlocked;
        public event Action<SkillData> OnSkillLocked;
        public event Action<string> OnMilestoneCompleted;
        public event Action<string, int, int> OnMilestoneProgressUpdated;
        public event Action OnProgressionDataLoaded;
        
        public bool IsSkillUnlocked(SkillData skillData)
        {
            return skillData != null && state.unlockedSkills.Contains(skillData.skillID);
        }

        public bool IsSkillUnlocked(string skillId)
        {
            return !string.IsNullOrEmpty(skillId) && state.unlockedSkills.Contains(skillId);
        }

        public void UnlockSkill(SkillData skillData)
        {
            if (skillData == null || string.IsNullOrEmpty(skillData.skillID)) return;

            string skillId = skillData.skillID;
            if (!skillUnlockRefCount.ContainsKey(skillId))
            {
                skillUnlockRefCount[skillId] = 0;
            }

            skillUnlockRefCount[skillId]++;

            if (!state.unlockedSkills.Contains(skillId))
            {
                state.unlockedSkills.Add(skillId);
                OnSkillUnlocked?.Invoke(skillData);
            }
        }

        public void LockSkill(SkillData skillData)
        {
            if (skillData == null || string.IsNullOrEmpty(skillData.skillID)) return;
            LockSkill(skillData.skillID, skillData);
        }

        public void LockSkill(string skillId)
        {
            LockSkill(skillId, null);
        }

        private void LockSkill(string skillId, SkillData skillData)
        {
            if (string.IsNullOrEmpty(skillId)) return;

            if (skillUnlockRefCount.ContainsKey(skillId))
            {
                skillUnlockRefCount[skillId]--;
                if (skillUnlockRefCount[skillId] <= 0)
                {
                    skillUnlockRefCount.Remove(skillId);
                    if (state.unlockedSkills.Contains(skillId))
                    {
                        state.unlockedSkills.Remove(skillId);
                        OnSkillLocked?.Invoke(skillData);
                    }
                }
            }
            else
            {
                if (state.unlockedSkills.Contains(skillId))
                {
                    state.unlockedSkills.Remove(skillId);
                    OnSkillLocked?.Invoke(skillData);
                }
            }
        }

        // --- 투사체 기록 로직 ---
        public void RecordProjectile(GameObject projectilePrefab)
        {
            if (projectilePrefab == null) return;

            string projectileTag = projectilePrefab.name;
            if (!state.unlockedProjectiles.Contains(projectileTag))
            {
                state.unlockedProjectiles.Add(projectileTag);
                if (!unlockedProjectilePrefabs.Contains(projectilePrefab))
                {
                    unlockedProjectilePrefabs.Add(projectilePrefab);
                }
                Debug.Log($"[Progression] 새로운 투사체 기록됨: {projectileTag}");
            }
        }

        public void RecordProjectile(string projectileTag)
        {
            if (!string.IsNullOrEmpty(projectileTag) && !state.unlockedProjectiles.Contains(projectileTag))
            {
                state.unlockedProjectiles.Add(projectileTag);
                Debug.Log($"[Progression] 새로운 투사체 태그 기록됨: {projectileTag}");
            }
        }

        public System.Collections.Generic.List<GameObject> GetUnlockedProjectilePrefabs()
        {
            return new System.Collections.Generic.List<GameObject>(unlockedProjectilePrefabs);
        }

        public System.Collections.Generic.List<string> GetUnlockedProjectiles()
        {
            return new System.Collections.Generic.List<string>(state.unlockedProjectiles);
        }
        // ------------------------------
        
        public bool IsMilestoneCompleted(string milestoneId)
        {
            return state.completedMilestones.Contains(milestoneId);
        }

        public void CompleteMilestone(string milestoneId)
        {
            if (!state.completedMilestones.Contains(milestoneId))
            {
                state.completedMilestones.Add(milestoneId);
                Debug.Log($"[Progression] 마일스톤 완료: {milestoneId}");

                OnMilestoneCompleted?.Invoke(milestoneId);
            }
        }

        public void CompleteMilestone(MilestoneData milestone)
        {
            if (milestone == null) return;

            if (!state.completedMilestones.Contains(milestone.milestoneID))
            {
                state.completedMilestones.Add(milestone.milestoneID);

                if (milestone.rewards != null)
                {
                    foreach (var reward in milestone.rewards)
                    {
                        GiveReward(reward);
                    }
                }

                OnMilestoneCompleted?.Invoke(milestone.milestoneID);
            }
        }

        private void GiveReward(RewardData reward)
        {
            switch (reward.rewardType)
            {
                case RewardType.Skill:
                    if (reward.skillData != null) UnlockSkill(reward.skillData);
                    break;
                case RewardType.Gold:
                    if (currencyDataManager != null) currencyDataManager.AddCurrency(CurrencyType.Gold, reward.amount);
                    break;
                case RewardType.Token:
                    if (currencyDataManager != null) currencyDataManager.AddCurrency(CurrencyType.Token, reward.amount);
                    break;
                case RewardType.Item:
                    if (inventoryDataManager != null && reward.itemData != null) inventoryDataManager.AddItem(reward.itemData, reward.amount);
                    break;
                case RewardType.Relic:
                    if (relicManager != null && reward.relicData != null) relicManager.AddNewRelicToStorage(reward.relicData);
                    break;
            }
        }
        
        public bool IsMilestoneAvailable(MilestoneData milestone)
        {
            if (milestone == null) return false;

            if (IsMilestoneCompleted(milestone.milestoneID)) return false;

            if (milestone.requiredMilestones != null)
            {
                foreach (var req in milestone.requiredMilestones)
                {
                    if (req != null && !IsMilestoneCompleted(req.milestoneID))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public int GetCurrentProgress(string milestoneId)
        {
            if (IsMilestoneCompleted(milestoneId)) return -1; 

            var progress = state.activeProgresses.Find(p => p.milestoneID == milestoneId);
            return progress != null ? progress.currentValue : 0;
        }

        public void AddProgress(MilestoneData milestone, int amount = 1)
        {
            if (milestone == null || IsMilestoneCompleted(milestone.milestoneID)) return;

            var progress = state.activeProgresses.Find(p => p.milestoneID == milestone.milestoneID);
            if (progress == null)
            {
                progress = new MilestoneProgressEntry { milestoneID = milestone.milestoneID, currentValue = 0 };
                state.activeProgresses.Add(progress);
            }

            progress.currentValue += amount;

            if (progress.currentValue > milestone.targetValue)
                progress.currentValue = milestone.targetValue;


            OnMilestoneProgressUpdated?.Invoke(milestone.milestoneID, progress.currentValue, milestone.targetValue);

            if (progress.currentValue >= milestone.targetValue)
            {
                CompleteMilestone(milestone); 

                state.activeProgresses.Remove(progress);
            }
        }

        public void ProcessAction(ProgressionType type, int amount = 1)
        {
            if (!milestonesByType.ContainsKey(type)) return;

            System.Collections.Generic.List<MilestoneData> milestones = milestonesByType[type];
            foreach (MilestoneData milestone in milestones)
            {
                if (IsMilestoneAvailable(milestone))
                {
                    AddProgress(milestone, amount);
                }
            }
        }

        public System.Collections.Generic.List<MilestoneData> GetAllMilestones()
        {
            return milestoneDatabase != null && milestoneDatabase.allMilestones != null 
                ? new System.Collections.Generic.List<MilestoneData>(milestoneDatabase.allMilestones) 
                : new System.Collections.Generic.List<MilestoneData>();
        }
        // --- ISaveable ---
        public override void PopulateSaveData(SaveData saveData)
        {
            saveData.progressionState = this.state;
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            if (saveData.progressionState != null)
            {
                this.state = saveData.progressionState;
                OnProgressionDataLoaded?.Invoke();
            }
        }
    }
}
