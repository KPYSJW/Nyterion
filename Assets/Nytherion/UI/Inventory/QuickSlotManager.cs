using System;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Data.ScriptableObjects.Items;
using UnityEngine;
using VContainer;

namespace Nytherion.UI.Inventory
{
    /// <summary>장착 스킬 데이터를 HUD의 Q/R/E 슬롯에 동기화합니다.</summary>
    public class QuickSlotManager : MonoBehaviour, ISaveable
    {
        private QuickSlotUI[] slots = Array.Empty<QuickSlotUI>();
        private SkillDataManager skillDataManager;
        private InventoryDataManager inventoryDataManager;

        public event Action OnQuickSlotUpdated;

        [Inject]
        public void Construct(GameSceneUIRefs uiRefs, SkillDataManager skillDataManager,
            InventoryDataManager inventoryDataManager)
        {
            if (this.skillDataManager != null)
                this.skillDataManager.OnSkillDataChanged -= RefreshSlots;

            this.skillDataManager = skillDataManager;
            this.inventoryDataManager = inventoryDataManager;
            this.skillDataManager.OnSkillDataChanged += RefreshSlots;
            TryInjectSlots(uiRefs.QuickSlots);
        }

        public void TryInjectSlots(QuickSlotUI[] quickSlots)
        {
            slots = quickSlots ?? Array.Empty<QuickSlotUI>();
            for (int i = 0; i < slots.Length; i++)
                slots[i]?.Initialize(i);
            RefreshSlots();
        }

        private void Start() => RefreshSlots();

        private void OnDestroy()
        {
            if (skillDataManager != null)
                skillDataManager.OnSkillDataChanged -= RefreshSlots;
        }

        public void RefreshSlots()
        {
            if (skillDataManager == null) return;
            var equipped = skillDataManager.equippedSkills;
            for (int i = 0; i < slots.Length; i++)
                slots[i]?.SetSkill(equipped != null && i < equipped.Length ? equipped[i] : null);
            OnQuickSlotUpdated?.Invoke();
        }

        // 기존 인벤토리 드롭 코드와의 호환용입니다. HUD에는 아이템을 저장하지 않습니다.
        public void UpdateSlotDataExternal(int index, ItemData item, int count) => RefreshSlots();

        public void PopulateSaveData(SaveData saveData)
        {
            // 장착 스킬은 SkillDataManager가 저장합니다.
            // 복원하지 못한 레거시 퀵슬롯 아이템은 기존 저장 항목을 그대로 보존합니다.
        }

        public void LoadFromSaveData(SaveData saveData)
        {
            // 예전 퀵슬롯의 소모품은 인벤토리로 돌려주고, 공간이 부족하면 저장 항목을 보존합니다.
            if (saveData?.quickSlotData != null && inventoryDataManager != null &&
                inventoryDataManager.IsInitialized)
            {
                for (int i = saveData.quickSlotData.Count - 1; i >= 0; i--)
                {
                    QuickSlotEntry entry = saveData.quickSlotData[i];
                    ItemData item = ItemDatabase.GetItemByID(entry.itemId);
                    if (!(item is ConsumableData) || entry.count <= 0) continue;
                    if (!item.isStackable)
                    {
                        item = Instantiate(item);
                        item.instanceId = string.IsNullOrEmpty(entry.instanceId)
                            ? Guid.NewGuid().ToString() : entry.instanceId;
                    }

                    // 로드 중 자동 저장을 호출하지 않고 원자적으로 수량 전체를 이동합니다.
                    if (inventoryDataManager.InventoryModel.AddItem(item, entry.count))
                        saveData.quickSlotData.RemoveAt(i);
                    else if (!item.isStackable)
                        Destroy(item);
                }
            }
            RefreshSlots();
        }
    }
}
