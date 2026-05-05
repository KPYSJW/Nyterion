using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using System;
using VContainer;
using System.Collections.Generic;


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
        private PlayerController playerController;

        [Header("Player Data")]
        [SerializeField] private PlayerData basePlayerData;
        public PlayerData currentPlayerData;
        public event Action OnPlayerStatsChanged;

        public int CurrentRunKillCount { get; private set; }

        [Inject]
        public void Construct(EquipmentDataManager equipmentDataManager, InputManager inputManager, EventManager eventManager)
        {
            this.equipmentDataManager = equipmentDataManager;
            this.inputManager = inputManager;
            this.eventManager = eventManager;
        }

        protected override void OnInitializeInternal()
        {
            
            playerHealth = GetComponent<PlayerHealth>();
            PlayerCombat = GetComponent<PlayerCombat>();
            playerRelicManager = GetComponent<PlayerRelicManager>();
            playerController = GetComponent<PlayerController>();

            if (playerRelicManager != null)
            {
                playerRelicManager.OnRelicsChanged += RecalculateStats;
            }

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
                eventManager.OnEnemyDamagedByPlayer += HandleEnemyDamaged;
                eventManager.OnEnemyDied += HandleEnemyDied;
            }

            CurrentRunKillCount = 0;
            RecalculateStats();
        }

        private void HandlePlayerDied()
        {
            CurrentRunKillCount = 0;
        }

        private void HandleEnemyDamaged(float damageAmount)
        {
            if (currentPlayerData != null && currentPlayerData.lifesteal > 0 && playerHealth != null)
            {
                float healAmount = damageAmount * currentPlayerData.lifesteal;
                if (healAmount > 0)
                {
                    playerHealth.Heal(healAmount);
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
            if (equipmentDataManager != null)
            {
                equipmentDataManager.OnEquipmentChanged += HandleEquipmentChanged;
            }
        }

        private void OnDisable()
        {
            if (equipmentDataManager != null)
            {
                equipmentDataManager.OnEquipmentChanged -= HandleEquipmentChanged;
            }
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
                eventManager.OnEnemyDamagedByPlayer -= HandleEnemyDamaged;
                eventManager.OnEnemyDied -= HandleEnemyDied;
            }
        }

        private void HandleEquipmentChanged(EquipmentSlotType slotType, EquipmentData newItem, EquipmentData oldItem)
        {
            RecalculateStats();

            if (newItem != null && newItem is WeaponData weaponData)
            {
                PlayerCombat?.EquipWeapon(weaponData.weaponPrefab);
            }
            else if (slotType == EquipmentSlotType.Weapon)
            {
                PlayerCombat?.EquipWeapon(null);
            }
        }

        private List<StatModifier> temporaryModifiers = new List<StatModifier>();

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
                    if (equippedItem != null && equippedItem.statModifiers != null)
                    {
                        allModifiers.AddRange(equippedItem.statModifiers);
                    }
                }
            }

            if (temporaryModifiers != null)
            {
                allModifiers.AddRange(temporaryModifiers);
            }

            // 1. 플랫(고정 수치) 증가치 먼저 적용
            foreach (var mod in allModifiers)
            {
                if (!mod.isPercentage)
                {
                    ApplyModifierToPlayer(mod.stat, mod.value, false);
                }
            }

            // 2. 퍼센트(비율) 증가치 합산 후 적용
            Dictionary<StatType, float> percentageSums = new Dictionary<StatType, float>();
            foreach (var mod in allModifiers)
            {
                if (mod.isPercentage)
                {
                    if (!percentageSums.ContainsKey(mod.stat))
                        percentageSums[mod.stat] = 0f;
                    percentageSums[mod.stat] += mod.value;
                }
            }

            foreach (var kvp in percentageSums)
            {
                ApplyModifierToPlayer(kvp.Key, kvp.Value, true);
            }

            if (playerHealth != null) playerHealth.UpdateMaxHealth(currentPlayerData.maxHealth);
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
                case StatType.ChargeTimeReduction:
                    if (isPercentage) currentPlayerData.chargeTimeReduction *= (1 + value);
                    else currentPlayerData.chargeTimeReduction += value;
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