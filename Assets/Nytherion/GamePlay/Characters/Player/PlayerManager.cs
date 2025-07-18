using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Data;
using System;
using Zenject;

namespace Nytherion.GamePlay.Characters.Player
{
    [System.Serializable]
    public class PlayerManager : MonoBehaviour
    {
        public PlayerHealth playerHealth { get; private set; }
        public PlayerCombat PlayerCombat { get; private set; }
        public PlayerEngravingManager playerEngravingManager { get; private set; }
        
        private EquipmentDataManager _equipmentDataManager;

        [Header("Player Data")]
        [SerializeField] private PlayerData basePlayerData;
        public PlayerData currentPlayerData;
        public event Action OnPlayerStatsChanged;

        [Inject]
        public void Construct(EquipmentDataManager equipmentDataManager)
        {
            _equipmentDataManager = equipmentDataManager;
        }

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            PlayerCombat = GetComponent<PlayerCombat>();
            playerEngravingManager = GetComponent<PlayerEngravingManager>();

            currentPlayerData = Instantiate(basePlayerData);
        }

        public void Initialize()
        {
            RecalculateStats();
        }

        private void OnEnable()
        {
            if (_equipmentDataManager != null)
            {
                _equipmentDataManager.OnEquipmentChanged += HandleEquipmentChanged;
            }
        }

        private void OnDisable()
        {
            if (_equipmentDataManager != null)
            {
                _equipmentDataManager.OnEquipmentChanged -= HandleEquipmentChanged;
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

            if (_equipmentDataManager != null)
            {
                foreach (var equippedItem in _equipmentDataManager.EquippedItems.Values)
                {
                    if (equippedItem != null)
                    {
                        ApplyStats(equippedItem);
                    }
                }
            }

            if (playerHealth != null) playerHealth.UpdateMaxHealth(currentPlayerData.maxHealth);
            OnPlayerStatsChanged?.Invoke();

            Debug.Log("[PlayerManager] 모든 능력치를 새로고침했습니다.");
        }

        private void ApplyStats(EquipmentData item)
        {
            if (item.statModifiers == null) return;
            foreach (StatModifier modifier in item.statModifiers)
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
    }
}