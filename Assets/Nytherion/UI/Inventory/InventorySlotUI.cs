using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Core.Managers;
using System;
using Nytherion.UI.Inventory.Utils;
using Nytherion.UI.Controllers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Enums;
using Zenject;

namespace Nytherion.UI.Inventory
{
    public class InventorySlotUI : BaseSlotUI, IDropHandler
    {
        public int SlotIndex { get; private set; }
        public event Action<BaseSlotUI> OnSellItemAction;

        private EquipmentDataManager equipmentDataManager;
        private ItemUsageManager itemUsageManager;
        private InventoryManager inventoryManager;
        private ShopUI shopUI;

        [Inject]
        public void Construct(
            EquipmentDataManager equipmentDataManager,
            ItemUsageManager itemUsageManager,
            InventoryManager inventoryManager,
            ShopUI shopUI)
        {
            this.equipmentDataManager = equipmentDataManager;
            this.itemUsageManager = itemUsageManager;
            this.inventoryManager = inventoryManager;
            this.shopUI = shopUI;
        }
        protected override void Awake()
        {
            base.Awake();
            OnBeginDragEvent += (s, e) => DragDropUIHandler.HandleBeginDragShared(s);
            OnEndDragEvent += (s, e) => DragDropUIHandler.HandleEndDragShared(s, e);
            OnPointerClickEvent += HandlePointerClick;
        }
        public void Initialize(int index)
        {
            SlotIndex = index;
            ClearSlot();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;
            BaseSlotUI sourceSlot = eventData.pointerDrag.GetComponent<BaseSlotUI>();
            if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot == this) return;

            if (sourceSlot is InventorySlotUI inventorySourceSlot)
            {
                inventoryManager.SwapItems(inventorySourceSlot.SlotIndex, this.SlotIndex);
            }
            else if (sourceSlot is EquipmentSlotUI equipmentSourceSlot)
            {
                // 유효성 검사 추가
                if (equipmentSourceSlot == null || equipmentSourceSlot.IsEmpty) return;
                
                try
                {
                    var (itemToUnequip, _) = equipmentSourceSlot.GetItemInfo();
                    if (itemToUnequip == null) return;

                    // 인벤토리 슬롯에 있던 아이템 (없으면 null)
                    ItemData itemInThisSlot = CurrentItem;
                    int countInThisSlot = CurrentCount;

                    // 1. 먼저 장비 슬롯을 비웁니다.
                    equipmentDataManager.SetEquipment(equipmentSourceSlot.SlotType, null);

                    // 2. 인벤토리 슬롯으로 아이템 이동
                    if (inventoryManager != null && inventoryManager.InventoryModel != null)
                    {
                        bool addSuccess = inventoryManager.InventoryModel.AddItemToSlot(itemToUnequip, 1, this.SlotIndex, true);
                        
                        // 3. 원래 인벤토리 슬롯에 아이템이 있었다면 해당 아이템을 장비 슬롯에 장착
                        if (addSuccess && itemInThisSlot != null && 
                            equipmentSourceSlot.CanReceiveItem(itemInThisSlot as EquipmentData))
                        {
                            equipmentDataManager.SetEquipment(equipmentSourceSlot.SlotType, itemInThisSlot as EquipmentData);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in OnDrop (EquipmentSlot): {e.Message}");
                    return;
                }
            }
            else if (sourceSlot is QuickSlotUI quickSourceSlot)
            {
                var (itemToMove, countToMove) = quickSourceSlot.GetItemInfo();

                if (itemToMove == null) return;

                if (this.IsEmpty)
                {
                    quickSourceSlot.ClearSlot();

                    inventoryManager.InventoryModel.AddItemToSlot(itemToMove, countToMove, this.SlotIndex);
                }
                else
                {
                    var (itemInThisSlot, countInThisSlot) = this.GetItemInfo();

                    if (quickSourceSlot.CanReceiveItem(itemInThisSlot))
                    {
                        quickSourceSlot.SetItem(itemInThisSlot, countInThisSlot);

                        inventoryManager.InventoryModel.AddItemToSlot(itemToMove, countToMove, this.SlotIndex);
                    }
                }

                DragDropUIHandler.dropHandled = true;
            }
        }

        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (IsEmpty || eventData.button != PointerEventData.InputButton.Right) return;

            if (shopUI != null && shopUI.IsOpen)
            {
                OnSellItemAction?.Invoke(this);
            }
            else if (CurrentItem is EquipmentData equipment)
            {
                EquipmentSlotType targetSlotType;

                if (equipment.equipmentType == EquipmentType.Weapon)
                {
                    targetSlotType = EquipmentSlotType.Weapon;
                }
                else if (equipment is ArmorData armor)
                {
                    switch (armor.armorType)
                    {
                        case ArmorType.Helmet: targetSlotType = EquipmentSlotType.Helmet; break;
                        case ArmorType.Armor: targetSlotType = EquipmentSlotType.Armor; break;
                        case ArmorType.Boots: targetSlotType = EquipmentSlotType.Boots; break;
                        case ArmorType.Accessory: targetSlotType = EquipmentSlotType.Accessory; break;
                        default: return;
                    }
                }
                else
                {
                    return;
                }

                if (inventoryManager.RemoveItemFromSlot(SlotIndex, 1))
                {
                    EquipmentData previouslyEquipped = equipmentDataManager.GetEquipment(targetSlotType);

                    equipmentDataManager.SetEquipment(targetSlotType, equipment);

                    if (previouslyEquipped != null)
                    {
                        inventoryManager.AddItem(previouslyEquipped, 1);
                    }
                }
            }
            else if (CurrentItem is ConsumableData consumable)
            {
                itemUsageManager.UseConsumableItem(consumable);
            }
            else
            {
                Debug.Log($"[Inventory] Used Item: {CurrentItem.itemName}");
            }
        }
    }
}