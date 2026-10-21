using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using System;
using VContainer;
using System.Collections.Generic;
using Nytherion.Core.Systems;


namespace Nytherion.Core.Managers
{
    public class PlayerManager : BaseManager, ISaveable
    {
        public PlayerHealth playerHealth { get; private set; }
        public PlayerCombat PlayerCombat { get; private set; }
        public PlayerRelicManager playerRelicManager { get; private set; }

        private EquipmentDataManager equipmentDataManager;
        private InputManager inputManager;
        private EventManager eventManager;
        private RelicManager relicManager;
        private SkillDataManager skillDataManager;
        private PlayerController playerController;
        public EventManager EventManager => eventManager;
        private IObjectResolver objectResolver;
        public ObjectPoolManager ObjectPool => objectResolver != null &&
            objectResolver.TryResolve<ObjectPoolManager>(out var pool) ? pool : null;

        [Header("Player Data")]
        [SerializeField] private PlayerData basePlayerData;
        public PlayerData currentPlayerData;
        public event Action OnPlayerStatsChanged;

        public int CurrentRunKillCount { get; private set; }

        private int luckyCloverResetCountInBattle = 0;

        public void ResetLuckyCloverResetCount()
        {
            luckyCloverResetCountInBattle = 0;
        }

        [Inject]
        public void Construct(
            EquipmentDataManager equipmentDataManager,
            InputManager inputManager,
            EventManager eventManager,
            RelicManager relicManager,
            SkillDataManager skillDataManager = null,
            IObjectResolver objectResolver = null)
        {
            UnsubscribeEquipmentChanges();
            this.equipmentDataManager = equipmentDataManager;
            this.inputManager = inputManager;
            this.eventManager = eventManager;
            this.relicManager = relicManager;
            this.skillDataManager = skillDataManager;
            this.objectResolver = objectResolver;
            if (isActiveAndEnabled)
            {
                SubscribeEquipmentChanges();
                if (IsInitialized) SynchronizeEquipment();
            }
        }

        public int GetSkillLevel(SkillData skill)
        {
            if (skill == null) return 1;
            if (skillDataManager != null && !string.IsNullOrEmpty(skill.skillID) &&
                skillDataManager.skillStates.TryGetValue(skill.skillID, out SkillState state))
                return Mathf.Max(1, state.level);
            return Mathf.Max(1, skill.skillLevel);
        }

        protected override void OnInitializeInternal()
        {
            
            playerHealth = GetComponent<PlayerHealth>();
            PlayerCombat = GetComponent<PlayerCombat>();
            playerRelicManager = GetComponent<PlayerRelicManager>();
            playerController = GetComponent<PlayerController>();

            if (playerRelicManager != null)
            {
                playerRelicManager.Construct(eventManager, relicManager);
                playerRelicManager.OnRelicsChanged += RecalculateStats;
            }

            RelicEffectController relicEffects = GetComponent<RelicEffectController>();
            if (relicEffects != null) relicEffects.Construct(relicManager);

            if (basePlayerData == null)
            {
                return;
            }

            currentPlayerData = Instantiate(basePlayerData);

            if (playerHealth != null)
            {
                playerHealth.InitializeHealth(currentPlayerData.maxHealth);
                PlayerHealth.OnPlayerDied += HandlePlayerDied;
            }

            if (playerController != null && inputManager != null)
            {
                playerController.Construct(inputManager, this);
            }
            if (PlayerCombat != null && inputManager != null)
            {
                PlayerCombat.Construct(inputManager);
            }

            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerWithCrit += HandleEnemyDamagedWithCrit;
                eventManager.OnEnemyDied += HandleEnemyDied;
            }

            CurrentRunKillCount = 0;
            SubscribeEquipmentChanges();
            // 타이틀에서 먼저 로드된 장비는 변경 알림을 다시 보내지 않으므로 현재 상태를 반영한다.
            SynchronizeEquipment();
        }

        private void HandlePlayerDied()
        {
            CurrentRunKillCount = 0;
        }

        private void HandleEnemyDamagedWithCrit(float damageAmount, bool isCritical)
        {
            if (currentPlayerData != null && currentPlayerData.lifesteal > 0 && playerHealth != null)
            {
                float healAmount = damageAmount * currentPlayerData.lifesteal;
                if (healAmount > 0)
                {
                    playerHealth.Heal(healAmount);
                }
            }

            // 네잎클로버 (LuckyClover) 유물 효과 적용: 치명타 적중 시 5% 확률로 대쉬 쿨타임 초기화
            if (isCritical && playerRelicManager != null && playerRelicManager.IsRelicActive("LuckyClover"))
            {
                if (UnityEngine.Random.Range(0, 100) < 5)
                {
                    if (playerController != null)
                    {
                        playerController.LastDashTime = -999f;
                        Debug.Log("[PlayerManager] 치명타 적중! 네잎클로버(LuckyClover) 효과 발동! 대쉬 쿨타임 초기화!");

                        luckyCloverResetCountInBattle++;
                        if (luckyCloverResetCountInBattle >= 3)
                        {
                            ProgressionManager progressionManager = DataLifetimeScope.Instance != null ? DataLifetimeScope.Instance.GetDataManager<ProgressionManager>() : null;
                            if (progressionManager != null)
                            {
                                progressionManager.ProcessAction(ProgressionType.LuckyCloverResetInOneBattle, 1);
                            }
                        }
                    }
                }
            }
        }

        private void HandleEnemyDied(Nytherion.GamePlay.Characters.Enemy.EnemyBase enemy)
        {
            CurrentRunKillCount++;
            OnPlayerStatsChanged?.Invoke(); // Notify to recalculate growth relics if needed
        }

        private void OnEnable()
        {
            SubscribeEquipmentChanges();
            if (IsInitialized) SynchronizeEquipment();
        }

        private void SubscribeEquipmentChanges()
        {
            if (equipmentDataManager != null)
            {
                UnsubscribeEquipmentChanges();
                equipmentDataManager.OnEquipmentChanged += HandleEquipmentChanged;
            }
        }

        private void UnsubscribeEquipmentChanges()
        {
            if (equipmentDataManager != null)
            {
                equipmentDataManager.OnEquipmentChanged -= HandleEquipmentChanged;
            }
        }

        private void OnDisable()
        {
            UnsubscribeEquipmentChanges();
            if (playerRelicManager != null)
            {
                playerRelicManager.OnRelicsChanged -= RecalculateStats;
            }
            if (playerHealth != null)
            {
                PlayerHealth.OnPlayerDied -= HandlePlayerDied;
            }
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerWithCrit -= HandleEnemyDamagedWithCrit;
                eventManager.OnEnemyDied -= HandleEnemyDied;
            }
        }

        private void HandleEquipmentChanged(EquipmentSlotType slotType, EquipmentData newItem, EquipmentData oldItem)
        {
            if (!IsInitialized) return;
            RecalculateStats();
            if (slotType == EquipmentSlotType.Weapon) SynchronizeWeapon();
        }

        private void SynchronizeEquipment()
        {
            RecalculateStats();
            SynchronizeWeapon();
        }

        private void SynchronizeWeapon()
        {
            if (PlayerCombat == null || equipmentDataManager == null) return;
            var weaponData = equipmentDataManager.GetEquipment(EquipmentSlotType.Weapon) as WeaponData;
            if (weaponData == null)
            {
                if (PlayerCombat.currentWeapon != null) PlayerCombat.EquipWeapon(null);
            }
            else if (PlayerCombat.currentWeapon == null || PlayerCombat.currentWeapon.weaponData != weaponData)
                PlayerCombat.EquipWeapon(weaponData.weaponPrefab, weaponData);
        }

        private List<StatModifier> temporaryModifiers = new List<StatModifier>();
        private bool debugGuaranteedCritical;

        public void SetDebugGuaranteedCritical(bool active)
        {
            if (debugGuaranteedCritical == active) return;
            debugGuaranteedCritical = active;
            if (currentPlayerData != null) RecalculateStats();
        }
        
        // 반전시켜야 할 장비 태그(Trait) 목록
        private List<EquipmentTrait> traitsToInvert = new List<EquipmentTrait>();

        public void AddTraitInversion(EquipmentTrait trait)
        {
            if (traitsToInvert.Contains(trait)) return;
            traitsToInvert.Add(trait);
            RecalculateStats();
        }

        public void RemoveTraitInversion(EquipmentTrait trait)
        {
            if (!traitsToInvert.Contains(trait)) return;
            traitsToInvert.Remove(trait);
            RecalculateStats();
        }

        public bool IsTraitInverted(EquipmentTrait trait)
        {
            return traitsToInvert.Contains(trait);
        }

        public void AddTemporaryStatModifier(StatModifier modifier)
        {
            temporaryModifiers.Add(modifier);
            RecalculateStats();
        }

        public void RemoveTemporaryStatModifier(StatModifier modifier)
        {
            temporaryModifiers.Remove(modifier);
            RecalculateStats();
        }

        private void RecalculateStats()
        {
            if (currentPlayerData == null)
            {
                currentPlayerData = Instantiate(basePlayerData);
            }
            else
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(basePlayerData), currentPlayerData);
            }

            List<StatModifier> allModifiers = new List<StatModifier>();

            if (equipmentDataManager != null)
            {
                foreach (var equippedItem in equipmentDataManager.EquippedItems.Values)
                {
                    if (equippedItem != null)
                    {
                        if (equippedItem is WeaponData weaponData)
                        {
                            StatType dmgStat = weaponData.weaponType == WeaponType.Melee ? StatType.MeleeDamage : StatType.RangedDamage;
                            allModifiers.Add(new StatModifier { stat = dmgStat, value = weaponData.damage, isPercentage = false });
                        }

                        if (equippedItem.statModifiers != null)
                        {
                            // 반전 대상 태그를 가지고 있는지 확인
                            bool shouldInvert = false;
                            if (equippedItem.traits != null)
                            {
                                foreach (var trait in equippedItem.traits)
                                {
                                    if (traitsToInvert.Contains(trait))
                                    {
                                        shouldInvert = true;
                                        break;
                                    }
                                }
                            }

                            foreach (var mod in equippedItem.statModifiers)
                            {
                                // 반전 대상 장비이면서, 스탯 값이 마이너스(-)인 경우에만 플러스로 반전
                                float finalValue = mod.value;
                                if (shouldInvert && finalValue < 0)
                                {
                                    finalValue = Mathf.Abs(finalValue);
                                }

                                allModifiers.Add(new StatModifier 
                                { 
                                    stat = mod.stat, 
                                    value = finalValue, 
                                    isPercentage = mod.isPercentage 
                                });
                            }
                        }
                    }
                }
            }

            if (temporaryModifiers != null)
            {
                allModifiers.AddRange(temporaryModifiers);
            }

            // 고정 수치 먼저 적용
            foreach (var mod in allModifiers)
            {
                if (!mod.isPercentage)
                {
                    if (mod.stat == StatType.All)
                    {
                        foreach (StatType st in Enum.GetValues(typeof(StatType)))
                        {
                            if (st != StatType.All) ApplyModifierToPlayer(st, mod.value, false);
                        }
                    }
                    else
                    {
                        ApplyModifierToPlayer(mod.stat, mod.value, false);
                    }
                }
            }

            // 퍼센트 증가치 합산 후 적용
            Dictionary<StatType, float> percentageSums = new Dictionary<StatType, float>();
            float allStatsPercentage = 0f;

            foreach (var mod in allModifiers)
            {
                if (mod.isPercentage)
                {
                    if (mod.stat == StatType.All)
                    {
                        allStatsPercentage += mod.value;
                    }
                    else
                    {
                        if (!percentageSums.ContainsKey(mod.stat))
                            percentageSums[mod.stat] = 0f;
                        percentageSums[mod.stat] += mod.value;
                    }
                }
            }

            // 모든 StatType에 대해 (All 제외) 합산된 퍼센트 적용
            foreach (StatType st in Enum.GetValues(typeof(StatType)))
            {
                if (st == StatType.All) continue;

                float specificSum = percentageSums.ContainsKey(st) ? percentageSums[st] : 0f;
                float totalSum = specificSum + allStatsPercentage;

                if (totalSum != 0)
                {
                    ApplyModifierToPlayer(st, totalSum, true);
                }
            }

            // 테스트 중에도 장비 변경이나 능력치 재계산 후 치명타 확률을 정확히 100%로 유지한다.
            if (debugGuaranteedCritical) currentPlayerData.critChance = 1f;

            // 유리 칼 (Glass Sword) 유물 효과 적용: 최대 체력 50 고정
            if (playerRelicManager != null && playerRelicManager.IsRelicActive("Glass Sword"))
            {
                currentPlayerData.maxHealth = 50f;
            }

            if (playerHealth != null) playerHealth.UpdateMaxHealth(currentPlayerData.maxHealth);

            // 이동 속도 및 대시 속도 최대치 상한선(Safety Cap) 적용하여 물리 엔진 오작동 방지
            currentPlayerData.moveSpeed = Mathf.Clamp(currentPlayerData.moveSpeed, 0f, 8.0f);
            currentPlayerData.dashSpeed = Mathf.Clamp(currentPlayerData.dashSpeed, 0f, 10.0f);

            OnPlayerStatsChanged?.Invoke();
        }

        private void ApplyModifierToPlayer(StatType stat, float value, bool isPercentage)
        {
            switch (stat)
            {
                case StatType.MaxHealth:
                    if (isPercentage) currentPlayerData.maxHealth *= (1 + value);
                    else currentPlayerData.maxHealth += value;
                    break;
                case StatType.Defense:
                    if (isPercentage) currentPlayerData.defense *= (1 + value);
                    else currentPlayerData.defense += value;
                    break;
                case StatType.MoveSpeed:
                    if (isPercentage) currentPlayerData.moveSpeed *= (1 + value);
                    else currentPlayerData.moveSpeed += value;
                    break;
                case StatType.MeleeDamage:
                    if (isPercentage) currentPlayerData.meleeDamage *= (1 + value);
                    else currentPlayerData.meleeDamage += value;
                    break;
                case StatType.RangedDamage:
                    if (isPercentage) currentPlayerData.rangedDamage *= (1 + value);
                    else currentPlayerData.rangedDamage += value;
                    break;
                case StatType.MeleeSpeed:
                    if (isPercentage) currentPlayerData.meleeSpeed *= (1 + value);
                    else currentPlayerData.meleeSpeed += value;
                    break;
                case StatType.RangedSpeed:
                    if (isPercentage) currentPlayerData.rangedSpeed *= (1 + value);
                    else currentPlayerData.rangedSpeed += value;
                    break;
                case StatType.DashSpeed:
                    if (isPercentage) currentPlayerData.dashSpeed *= (1 + value);
                    else currentPlayerData.dashSpeed += value;
                    break;
                case StatType.DashDuration:
                    if (isPercentage) currentPlayerData.dashDuration *= (1 + value);
                    else currentPlayerData.dashDuration += value;
                    break;
                case StatType.DashCooldown:
                    if (isPercentage) currentPlayerData.dashCooldown *= (1 + value);
                    else currentPlayerData.dashCooldown += value;
                    break;
                case StatType.ExtraProjectiles:
                    if (isPercentage) currentPlayerData.extraProjectiles *= (1 + value);
                    else currentPlayerData.extraProjectiles += value;
                    break;
                case StatType.ProjectileSize:
                    if (isPercentage) currentPlayerData.projectileSizeMultiplier *= (1 + value);
                    else currentPlayerData.projectileSizeMultiplier += value;
                    break;
                case StatType.ChargeTimeReduction:
                    if (isPercentage) currentPlayerData.chargeTimeReduction *= (1 + value);
                    else currentPlayerData.chargeTimeReduction += value;
                    break;
                case StatType.CritChance:
                    if (isPercentage) currentPlayerData.critChance *= (1 + value);
                    else currentPlayerData.critChance += value;
                    break;
                case StatType.CritDamage:
                    if (isPercentage) currentPlayerData.critDamageMultiplier *= (1 + value);
                    else currentPlayerData.critDamageMultiplier += value;
                    break;
                case StatType.Lifesteal:
                    if (isPercentage) currentPlayerData.lifesteal *= (1 + value);
                    else currentPlayerData.lifesteal += value;
                    break;
                default:
                    Debug.LogError($"[PlayerManager] Invalid stat type: {stat}");
                    break;
            }
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            if (saveData == null) return;

           
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            if (saveData == null) return;
        }
    }
}
