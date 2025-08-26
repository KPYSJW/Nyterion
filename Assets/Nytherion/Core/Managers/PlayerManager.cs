using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using System;
using Zenject;
using System.Collections.Generic;


namespace Nytherion.Core.Managers
{
    public class PlayerManager : BaseManager, ISaveable
    {
        public PlayerHealth playerHealth { get; private set; }
        public PlayerCombat PlayerCombat { get; private set; }
        public PlayerEngravingManager playerEngravingManager { get; private set; }

        private EquipmentDataManager equipmentDataManager;

        [Header("Player Data")]
        [SerializeField] private PlayerData basePlayerData;
        public PlayerData currentPlayerData;
        public event Action OnPlayerStatsChanged;

        [Inject]
        public void Construct(EquipmentDataManager equipmentDataManager)
        {
            this.equipmentDataManager = equipmentDataManager;
        }

        protected override void OnInitializeInternal()
        {
            playerHealth = GetComponent<PlayerHealth>();
            PlayerCombat = GetComponent<PlayerCombat>();
            playerEngravingManager = GetComponent<PlayerEngravingManager>();

            currentPlayerData = Instantiate(basePlayerData);
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

            if (equipmentDataManager != null)
            {
                foreach (var equippedItem in equipmentDataManager.EquippedItems.Values)
                {
                    if (equippedItem != null)
                    {
                        ApplyStatModifiers(equippedItem.statModifiers);
                    }
                }
            }
            if (playerEngravingManager != null)
            {
                var currentEngravings = playerEngravingManager.GetCurrentEngravings();
                foreach (var engraving in currentEngravings)
                {
                    if (engraving != null)
                    {
                        //ApplyStatModifiers(engraving.statModifiers);
                    }
                }
            }

            if (playerHealth != null) playerHealth.UpdateMaxHealth(currentPlayerData.maxHealth);
            OnPlayerStatsChanged?.Invoke();

            Debug.Log("[PlayerManager] 모든 능력치를 새로고침했습니다.");
        }

        private void ApplyStatModifiers(IEnumerable<StatModifier> modifiers)
        {
            if (modifiers == null) return;
            foreach (StatModifier modifier in modifiers)
            {
                ApplyModifierToPlayer(modifier.stat, modifier.value);
            }
        }

        private void ApplyModifierToPlayer(StatType stat, float value)
        {
            switch (stat)
            {
                case StatType.MaxHealth:
                    currentPlayerData.maxHealth += value;
                    break;
                case StatType.Defense:
                    currentPlayerData.defense += value;
                    break;
                case StatType.MoveSpeed:
                    currentPlayerData.moveSpeed += value;
                    break;
                case StatType.MeleeDamage:
                    currentPlayerData.meleeDamage += value;
                    break;
                case StatType.RangedDamage:
                    currentPlayerData.rangedDamage += value;
                    break;
                case StatType.MeleeSpeed:
                    currentPlayerData.meleeSpeed += value;
                    break;
                case StatType.RangedSpeed:
                    currentPlayerData.rangedSpeed += value;
                    break;
                case StatType.DashSpeed:
                    currentPlayerData.dashSpeed += value;
                    break;
                case StatType.DashDuration:
                    currentPlayerData.dashDuration += value;
                    break;
                case StatType.DashCooldown:
                    currentPlayerData.dashCooldown += value;
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