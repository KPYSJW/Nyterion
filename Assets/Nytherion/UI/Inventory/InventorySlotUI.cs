using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Core.Managers;
using System;
using Nytherion.UI.Inventory.Utils;
using Nytherion.UI.Controllers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Enums;

namespace Nytherion.UI.Inventory
{
    public class InventorySlotUI : BaseSlotUI, IDropHandler
    {
        public int SlotIndex { get; private set; }
        public event Action<BaseSlotUI> OnSellItemAction;

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
                InventoryManager.Instance.SwapItems(inventorySourceSlot.SlotIndex, this.SlotIndex);
            }
            else if (sourceSlot is EquipmentSlotUI equipmentSourceSlot)
            {
                var (equippedItem, equippedItemCount) = equipmentSourceSlot.GetItemInfo();
                var (inventoryItem, inventoryItemCount) = this.GetItemInfo();

                if (this.IsEmpty)
                {
                    equipmentSourceSlot.ClearSlot();
                    InventoryManager.Instance.InventoryModel.AddItemToSlot(equippedItem, equippedItemCount, this.SlotIndex);
                }
                else
                {
                    if (equipmentSourceSlot.CanReceiveItem(inventoryItem))
                    {
                        InventoryManager.Instance.RemoveItemFromSlot(this.SlotIndex, inventoryItemCount);
                        equipmentSourceSlot.SetItem(inventoryItem, inventoryItemCount);
                        InventoryManager.Instance.InventoryModel.AddItemToSlot(equippedItem, equippedItemCount, this.SlotIndex);
                    }
                }
            }
            else if (sourceSlot is QuickSlotUI quickSourceSlot)
            {
                var (itemToMove, countToMove) = quickSourceSlot.GetItemInfo();

                if (itemToMove == null) return;

                if (this.IsEmpty)
                {
                    quickSourceSlot.ClearSlot();

                    InventoryManager.Instance.InventoryModel.AddItemToSlot(itemToMove, countToMove, this.SlotIndex);
                }
                else
                {
                    var (itemInThisSlot, countInThisSlot) = this.GetItemInfo();

                    if (quickSourceSlot.CanReceiveItem(itemInThisSlot))
                    {
                        quickSourceSlot.SetItem(itemInThisSlot, countInThisSlot);

                        InventoryManager.Instance.InventoryModel.AddItemToSlot(itemToMove, countToMove, this.SlotIndex);
                    }
                }

                DragDropUIHandler.dropHandled = true;
            }
        }
        
        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (IsEmpty || eventData.button != PointerEventData.InputButton.Right) return;

            if (ShopUI.Instance != null && ShopUI.Instance.IsOpen)
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
                        case ArmorType.Helmet:    targetSlotType = EquipmentSlotType.Helmet; break;
                        case ArmorType.Armor:     targetSlotType = EquipmentSlotType.Armor; break;
                        case ArmorType.Boots:     targetSlotType = EquipmentSlotType.Boots; break;
                        case ArmorType.Accessory: targetSlotType = EquipmentSlotType.Accessory; break;
                        default: return;
                    }
                }
                else
                {
                    return;
                }

                if (InventoryManager.Instance.RemoveItemFromSlot(SlotIndex, 1))
                {
                    EquipmentData previouslyEquipped = EquipmentDataManager.Instance.GetEquipment(targetSlotType);

                    EquipmentDataManager.Instance.SetEquipment(targetSlotType, equipment);

                    if (previouslyEquipped != null)
                    {
                        InventoryManager.Instance.AddItem(previouslyEquipped, 1);
                    }
                }
            }
            else if(CurrentItem is ConsumableData consumable)
            {
                ItemUsageManager.Instance.UseConsumableItem(consumable);
            }
            else
            {
                Debug.Log($"[Inventory] Used Item: {currentItem.itemName}");
            }
        }
    }
}