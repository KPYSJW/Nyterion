using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Core.Managers;
using System;
using Nytherion.UI.Inventory.Utils;
using Nytherion.UI.Controllers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Enums;
using VContainer;
using VContainer.Unity;

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

        private void Start()
        {
            if (inventoryManager == null || equipmentDataManager == null || itemUsageManager == null || shopUI == null)
            {
                var gameSceneScope = LifetimeScope.Find<GameSceneLifetimeScope>();
                if (gameSceneScope != null)
                {
                    if (inventoryManager == null && gameSceneScope.Container.TryResolve<InventoryManager>(out var invManager))
                    {
                        inventoryManager = invManager;
                    }

                    if (equipmentDataManager == null && gameSceneScope.Container.TryResolve<EquipmentDataManager>(out var equipManager))
                    {
                        equipmentDataManager = equipManager;
                    }

                    if (itemUsageManager == null && gameSceneScope.Container.TryResolve<ItemUsageManager>(out var usageManager))
                    {
                        itemUsageManager = usageManager;
                    }

                    if (shopUI == null && gameSceneScope.Container.TryResolve<ShopUI>(out var shop))
                    {
                        shopUI = shop;
                    }
                }
            }
        }
        public void Initialize(int index)
        {
            SlotIndex = index;
            ClearSlot();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) 
            {
                return;
            }
            
            BaseSlotUI sourceSlot = eventData.pointerDrag.GetComponent<BaseSlotUI>();
            if (sourceSlot == null) 
            {
                return;
            }
            
            if (sourceSlot.IsEmpty) 
            {
                return;
            }
            
            if (sourceSlot == this) 
            {
                return;
            }
            
            if (inventoryManager == null)
            {
                inventoryManager = FindObjectOfType<InventoryManager>();
                if (inventoryManager == null)
                {
                    Debug.LogError("[InventorySlotUI] InventoryManager not found. Cannot perform drop operation.");
                    return;
                }
            }

            if (equipmentDataManager == null)
            {
                equipmentDataManager = FindObjectOfType<EquipmentDataManager>();
                if (equipmentDataManager == null)
                {
                    Debug.LogError("[InventorySlotUI] EquipmentDataManager not found. Equipment operations will be disabled.");
                }
            }

            if (sourceSlot is InventorySlotUI inventorySourceSlot)
            {
                inventoryManager.SwapItems(inventorySourceSlot.SlotIndex, this.SlotIndex);
            }
            else if (sourceSlot is EquipmentSlotUI equipmentSourceSlot)
            {
                if (equipmentSourceSlot == null || equipmentSourceSlot.IsEmpty) return;
                
                if (equipmentDataManager == null)
                {
                    Debug.LogError("[InventorySlotUI] EquipmentDataManager is still null. Cannot perform equipment drop operation.");
                    return;
                }
                
                try
                {
                    var (itemToUnequip, _) = equipmentSourceSlot.GetItemInfo();
                    if (itemToUnequip == null) return;

                    ItemData itemInThisSlot = CurrentItem;
                    int countInThisSlot = CurrentCount;

                    equipmentDataManager.SetEquipment(equipmentSourceSlot.SlotType, null);

                    if (inventoryManager != null && inventoryManager.InventoryModel != null)
                    {
                        bool addSuccess = inventoryManager.InventoryModel.AddItemToSlot(itemToUnequip, 1, this.SlotIndex, true);
                        
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

                    if (string.IsNullOrEmpty(equipment.instanceId))
                    {
                        equipment.instanceId = System.Guid.NewGuid().ToString();
                    }

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