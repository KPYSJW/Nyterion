using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEngine;
using Nytherion.Core.Enums;
using System.Collections.Generic;
using System;

namespace Nytherion.GamePlay.Characters.Player
{
    [System.Serializable]
    public class PlayerManager : MonoBehaviour
    {
        public static PlayerManager Instance { get; private set; }

        public PlayerHealth playerHealth;

        [SerializeField]
        private PlayerCombat playerCombat;

        public PlayerCombat PlayerCombat => playerCombat;

        public PlayerEngravingManager playerEngravingManager;
        public PlayerData playerData;

        private Dictionary<EquipmentSlotType, EquipmentData> equippedItems = new Dictionary<EquipmentSlotType, EquipmentData>();

        public IReadOnlyDictionary<EquipmentSlotType, EquipmentData> EquippedItems => equippedItems;

        public event Action<EquipmentSlotType, EquipmentData> OnEquipmentChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (playerCombat == null)
            {
                playerCombat = GetComponent<PlayerCombat>();
                if (playerCombat == null)
                {
                    Debug.LogError("PlayerCombat 컴포넌트를 찾을 수 없습니다.", this);
                }
            }
        }

        public void Initialize()
        {
            equippedItems.Clear();
        }

        public void EquipItem(EquipmentSlotType slotType, EquipmentData item)
        {
            if (equippedItems.ContainsKey(slotType) && equippedItems[slotType] != null)
            {
                UnequipStats(equippedItems[slotType]);
            }

            equippedItems[slotType] = item;

            if (item != null)
            {
                ApplyStats(item);

                if (item is WeaponData weaponData)
                {
                    if (PlayerCombat != null) PlayerCombat.EquipWeapon(weaponData.weaponPrefab);
                }
            }
            else
            {
                if (slotType == EquipmentSlotType.Weapon)
                {
                    if (PlayerCombat != null) PlayerCombat.EquipWeapon(null);
                }
            }
            OnEquipmentChanged?.Invoke(slotType, item);
        }

        private void ApplyStats(EquipmentData item)
        {
            if (item is ArmorData armor)
            {
                playerData.maxHealth += armor.defense;
            }
        }

        private void UnequipStats(EquipmentData item)
        {
            if (item is ArmorData armor)
            {
                playerData.maxHealth -= armor.defense;
            }
        }
        
    }
}