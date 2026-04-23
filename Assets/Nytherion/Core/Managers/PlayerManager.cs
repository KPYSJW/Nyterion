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
        public PlayerEngravingManager playerEngravingManager { get; private set; }

        private EquipmentDataManager equipmentDataManager;
        private InputManager inputManager;
        private PlayerController playerController;

        [Header("Player Data")]
        [SerializeField] private PlayerData basePlayerData;
        public PlayerData currentPlayerData;
        public event Action OnPlayerStatsChanged;

        [Inject]
        public void Construct(EquipmentDataManager equipmentDataManager, InputManager inputManager)
        {
            this.equipmentDataManager = equipmentDataManager;
            this.inputManager = inputManager;
        }

        protected override void OnInitializeInternal()
        {
            
            playerHealth = GetComponent<PlayerHealth>();
            PlayerCombat = GetComponent<PlayerCombat>();
            playerEngravingManager = GetComponent<PlayerEngravingManager>();
            playerController = GetComponent<PlayerController>();

            if (playerEngravingManager != null)
            {
                playerEngravingManager.OnEngravingsChanged += RecalculateStats;
            }

            if (basePlayerData == null)
            {
                return;
            }

            currentPlayerData = Instantiate(basePlayerData);

            if (playerHealth != null)
            {
                playerHealth.InitializeHealth(currentPlayerData.maxHealth);
            }

            if (playerController != null && inputManager != null)
            {
                playerController.Construct(inputManager, this);
            }
            if (PlayerCombat != null && inputManager != null)
            {
                PlayerCombat.Construct(inputManager);
            }

            RecalculateStats();
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
            if (playerEngravingManager != null)
            {
                playerEngravingManager.OnEngravingsChanged -= RecalculateStats;
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
            if (playerEngravingManager != null)
            {
                var currentEngravings = playerEngravingManager.GetCurrentEngravings();
                foreach (var engraving in currentEngravings)
                {
                    if (engraving != null && engraving.statModifiers != null)
                    {
                        allModifiers.AddRange(engraving.statModifiers);
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