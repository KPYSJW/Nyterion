using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Controllers;
using Nytherion.UI.Inventory.Utils;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Inventory
{
    public class InventorySlotUI : BaseSlotUI, IDropHandler
    {
        public int SlotIndex { get; private set; }
        //public event Action<BaseSlotUI> OnSellItemAction;
        private ShopAction input;

        private EquipmentDataManager equipmentDataManager;
        private ItemUsageManager itemUsageManager;
        private InventoryDataManager inventoryDataManager;
        private ShopUI shopUI;
        private QuickSlotManager quickSlotManager;

        [Inject]
        public void Construct(
            EquipmentDataManager equipmentDataManager,
            ItemUsageManager itemUsageManager,
            InventoryDataManager inventoryDataManager,
            ShopUI shopUI,
            QuickSlotManager quickSlotManager)
        {
            this.equipmentDataManager = equipmentDataManager;
            this.itemUsageManager = itemUsageManager;
            this.inventoryDataManager = inventoryDataManager;
            this.shopUI = shopUI;
            this.quickSlotManager = quickSlotManager;
        }
        protected override void Awake()
        {
            base.Awake();
            input = new ShopAction();
            OnBeginDragEvent += (s, e) => DragDropUIHandler.HandleBeginDragShared(s);
            OnEndDragEvent += (s, e) => DragDropUIHandler.HandleEndDragShared(s, e);
            OnPointerClickEvent += HandlePointerClick;
        }
        void OnEnable() => input?.Enable(); 
        void OnDisable() => input?.Disable();
        private void Start()
        {
            if (inventoryDataManager == null || equipmentDataManager == null || itemUsageManager == null || shopUI == null || quickSlotManager == null)
            {
                var gameSceneScope = LifetimeScope.Find<GameSceneLifetimeScope>();
                if (gameSceneScope != null)
                {
                    if (inventoryDataManager == null && gameSceneScope.Container.TryResolve<InventoryDataManager>(out var invManager))
                    {
                        inventoryDataManager = invManager;
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

                    if (quickSlotManager == null && gameSceneScope.Container.TryResolve<QuickSlotManager>(out var quickManager))
                    {
                        quickSlotManager = quickManager;
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
            if (eventData.button != PointerEventData.InputButton.Left) return;
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

            if (inventoryDataManager == null)
            {
                inventoryDataManager = FindObjectOfType<InventoryDataManager>();
                if (inventoryDataManager == null)
                {
                    Debug.LogError("[InventorySlotUI] InventoryDataManager not found. Cannot perform drop operation.");
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
                inventoryDataManager.SwapItems(inventorySourceSlot.SlotIndex, this.SlotIndex);
                DragDropUIHandler.dropHandled = true;
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
                    (ItemData itemToUnequip, int itemQuantity) = equipmentSourceSlot.GetItemInfo();
                    if (itemToUnequip == null) return;

                    ItemData itemInThisSlot = CurrentItem;

                    // 인벤토리 대상 슬롯에 이미 아이템이 있고 스왑할 수 없는 조건인 경우 취소하여 증발 방지
                    if (itemInThisSlot != null)
                    {
                        bool canSwap = itemInThisSlot is EquipmentData &&
                                       equipmentSourceSlot.CanReceiveItem(itemInThisSlot as EquipmentData);
                        if (!canSwap)
                        {
                            Debug.LogWarning("[InventorySlotUI] Cannot swap: target slot item is not compatible with equipment slot.");
                            return;
                        }
                    }

                    equipmentSourceSlot.ClearSlot();

                    if (inventoryDataManager != null)
                    {
                        bool addSuccess = inventoryDataManager.AddItemToSlot(itemToUnequip, 1, this.SlotIndex, true);

                        if (addSuccess)
                        {
                            this.SetItem(itemToUnequip, 1);

                            if (itemInThisSlot != null)
                            {
                                equipmentDataManager.SetEquipment(equipmentSourceSlot.SlotType, itemInThisSlot as EquipmentData, false);
                            }
                        }
                    }
                    DragDropUIHandler.dropHandled = true;
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
                    // 퀵슬롯에서 아이템 제거
                    quickSourceSlot.ClearSlot();

                    // QuickSlotManager에 변경사항 알림
                    if (quickSlotManager != null)
                    {
                        quickSlotManager.UpdateSlotDataExternal(quickSourceSlot.SlotIndex, null, 0);
                    }

                    // 인벤토리에 아이템 추가
                    inventoryDataManager.AddItemToSlot(itemToMove, countToMove, this.SlotIndex, true);
                }
                else
                {
                    var (itemInThisSlot, countInThisSlot) = this.GetItemInfo();

                    if (quickSourceSlot.CanReceiveItem(itemInThisSlot))
                    {
                        // 퀵슬롯에 현재 인벤토리 아이템 설정
                        quickSourceSlot.SetItem(itemInThisSlot, countInThisSlot);

                        // QuickSlotManager에 변경사항 알림
                        if (quickSlotManager != null)
                        {
                            quickSlotManager.UpdateSlotDataExternal(quickSourceSlot.SlotIndex, itemInThisSlot, countInThisSlot);
                        }

                        // 인벤토리에 퀵슬롯 아이템 추가
                        inventoryDataManager.AddItemToSlot(itemToMove, countToMove, this.SlotIndex, true);
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
                bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                if (isShiftPressed && CurrentCount > 1)
                {
                    shopUI.OpenSellPopup(CurrentItem, CurrentCount);
                }
                else
                {
                    shopUI.QuickSellItem(CurrentItem, 1);
                }

                return;
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

                if (inventoryDataManager.RemoveItemFromSlot(SlotIndex, 1))
                {
                    EquipmentData previouslyEquipped = equipmentDataManager.GetEquipment(targetSlotType);

                    if (string.IsNullOrEmpty(equipment.instanceId))
                    {
                        equipment.instanceId = System.Guid.NewGuid().ToString();
                    }

                    equipmentDataManager.SetEquipment(targetSlotType, equipment);

                    if (previouslyEquipped != null)
                    {
                        inventoryDataManager.AddItem(previouslyEquipped, 1);
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