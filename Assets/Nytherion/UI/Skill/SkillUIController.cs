using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using VContainer;
using System.Collections.Generic;

namespace Nytherion.UI.Skill
{
    public class SkillUIController : MonoBehaviour
    {
        [Header("Toggle Settings")]
        [SerializeField] private GameObject uiPanel;

        [Header("UI References")]
        [SerializeField] private SkillSlotUI[] equipSlots;
        [SerializeField] private Transform storageContent; 
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private int maxStorageSlots = 20;

        private InputManager inputManager;
        private SkillDataManager skillDataManager;
        private PlayerSkillManager playerSkillManager;
        private SaveLoadManager saveLoadManager;
        private SkillSlotUI[] storageSlots;

        [SerializeField] private SkillStorageArea storageDropArea;

        [Inject]
        public void Construct(
            SkillDataManager skillDataManager,
            PlayerSkillManager playerSkillManager,
            SaveLoadManager saveLoadManager, 
            InputManager inputManager)
        {
            this.skillDataManager = skillDataManager;
            this.playerSkillManager = playerSkillManager;
            this.saveLoadManager = saveLoadManager;
            this.inputManager = inputManager; 
        }

        private void Start()
        {
            InitializeStorageSlots();

            foreach (var slot in equipSlots)
            {
                slot.OnDoubleClick += HandleDoubleClick;
                slot.OnDropSkill += HandleDrop;
            }

            if (uiPanel != null) uiPanel.SetActive(false);

            if (skillDataManager != null)
            {
                skillDataManager.OnSkillDataChanged += SyncUIFromData;
                SyncUIFromData();
            }
            if (storageDropArea != null)
            {
                storageDropArea.OnDropToStorage += HandleDropToStorageBackground;
            }
            if (inputManager != null)
            {
                inputManager.onToggleSkillUI += ToggleUI;
            }
        }
        private void OnDestroy()
        {
            if (skillDataManager != null) skillDataManager.OnSkillDataChanged -= SyncUIFromData;
            if (inputManager != null) inputManager.onToggleSkillUI -= ToggleUI;
        }
        public void ToggleUI()
        {
            bool isActive = !uiPanel.activeSelf;
            uiPanel.SetActive(isActive);
            if (isActive) SyncUIFromData();
        }
        private void InitializeStorageSlots()
        {
            storageSlots = new SkillSlotUI[maxStorageSlots];
            for (int i = 0; i < maxStorageSlots; i++)
            {
                GameObject go = Instantiate(slotPrefab, storageContent);
                SkillSlotUI newSlot = go.GetComponent<SkillSlotUI>();

                newSlot.slotType = SkillSlotType.Storage;
                newSlot.slotIndex = i;
                newSlot.Setup(null);

                newSlot.OnDoubleClick += HandleDoubleClick;
                newSlot.OnDropSkill += HandleDrop;

                storageSlots[i] = newSlot;
            }
        }
        private void SyncUIFromData()
        {
            if (skillDataManager == null) return;

            for (int i = 0; i < equipSlots.Length; i++)
            {
                equipSlots[i].Setup(skillDataManager.equippedSkills[i]);
            }

            for (int i = 0; i < storageSlots.Length; i++)
            {
                if (i < skillDataManager.storageSkills.Length)
                    storageSlots[i].Setup(skillDataManager.storageSkills[i]);
            }

            if (playerSkillManager != null)
                playerSkillManager.SetEquippedSkills(skillDataManager.equippedSkills);
        }

        //public void RefreshStorageUI()
        //{
        //    if (skillDataManager == null)
        //    {
        //        Debug.LogError("SkillDataManager가 주입되지 않았습니다!");
        //        return;
        //    }

        //    foreach (var slot in instantiatedStorageSlots)
        //    {
        //        Destroy(slot.gameObject);
        //    }
        //    instantiatedStorageSlots.Clear();

        //    List<SkillData> unequippedSkills = new List<SkillData>();
        //    foreach (var skill in skillDataManager.ownedSkills)
        //    {
        //        if (skill != null && !IsEquipped(skill))
        //        {
        //            unequippedSkills.Add(skill);
        //        }
        //    }
        //    int slotsToCreate = Mathf.Max(maxStorageSlots, unequippedSkills.Count);

        //    for (int i = 0; i < slotsToCreate; i++)
        //    {
        //        GameObject go = Instantiate(slotPrefab, storageContent);
        //        SkillSlotUI newSlot = go.GetComponent<SkillSlotUI>();

        //        newSlot.slotType = SkillSlotType.Storage;
        //        newSlot.slotIndex = i;

        //        if (i < unequippedSkills.Count)
        //        {
        //            newSlot.Setup(unequippedSkills[i]);
        //        }
        //        else
        //        {
        //            newSlot.Setup(null); 
        //        }

        //        newSlot.OnDoubleClick += HandleDoubleClick;
        //        newSlot.OnDropSkill += HandleDrop;

        //        instantiatedStorageSlots.Add(newSlot);
        //    }

        //}

        private bool IsEquipped(SkillData skillToCheck)
        {
            if (skillToCheck == null) return false;
            foreach (var equippedSkill in skillDataManager.equippedSkills)
            {
                if (equippedSkill == skillToCheck) return true;
            }
            return false;
        }

        private void HandleDoubleClick(SkillSlotUI clickedSlot)
        {
            if (clickedSlot.slotType == SkillSlotType.Storage && clickedSlot.GetSkill() != null)
            {
                SkillSlotUI target = GetAvailableEquipSlot();
                SwapSkills(clickedSlot, target);
            }
            else if (clickedSlot.slotType == SkillSlotType.Equipped && clickedSlot.GetSkill() != null)
            {
                SkillSlotUI emptyStorageSlot = GetAvailableStorageSlot();
                if (emptyStorageSlot != null) SwapSkills(clickedSlot, emptyStorageSlot);
            }
        }

        private void HandleDrop(SkillSlotUI fromSlot, SkillSlotUI toSlot)
        {
            SwapSkills(fromSlot, toSlot);
        }
        private void HandleDropToStorageBackground(SkillSlotUI draggedEquipSlot)
        {
            SkillSlotUI emptyStorageSlot = GetAvailableStorageSlot();

            if (emptyStorageSlot != null)
            {
                SwapSkills(draggedEquipSlot, emptyStorageSlot);
            }
        }
        private void SwapSkills(SkillSlotUI slotA, SkillSlotUI slotB)
        {
            if (slotA == null || slotB == null) return;

            SkillData skillA = slotA.GetSkill();
            SkillData skillB = slotB.GetSkill();

            slotA.Setup(skillB);
            slotB.Setup(skillA);

            UpdatePlayerSkills(); 
            SyncUIFromData();
        }

        private SkillSlotUI GetAvailableEquipSlot()
        {
            foreach (var slot in equipSlots)
            {
                if (slot.GetSkill() == null) return slot;
            }
            return equipSlots[0];
        }

        private SkillSlotUI GetAvailableStorageSlot()
        {
            foreach (var slot in storageSlots) 
            {
                if (slot.GetSkill() == null) return slot;
            }
            return null;
        }

        private void UpdatePlayerSkills()
        {
            SkillData[] currentEquipped = new SkillData[equipSlots.Length];
            for (int i = 0; i < equipSlots.Length; i++)
            {
                currentEquipped[i] = equipSlots[i].GetSkill();
            }

            SkillData[] currentStorage = new SkillData[storageSlots.Length];
            int storageIndex = 0; 

            for (int i = 0; i < storageSlots.Length; i++)
            {
                SkillData skill = storageSlots[i].GetSkill();
                if (skill != null)
                {
                    currentStorage[storageIndex] = skill;
                    storageIndex++;
                }
            }

            if (skillDataManager != null)
            {
                skillDataManager.UpdateSkills(currentEquipped, currentStorage);
            }

            if (playerSkillManager != null)
            {
                playerSkillManager.SetEquippedSkills(currentEquipped);
            }

            if (saveLoadManager != null)
            {
                saveLoadManager.SaveGame();
            }
        }
    }
}