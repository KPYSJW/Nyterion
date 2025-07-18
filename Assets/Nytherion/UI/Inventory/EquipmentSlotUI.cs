using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory.Utils;
using Nytherion.Core.Enums;

namespace Nytherion.UI.Inventory
{
    public class EquipmentSlotUI : BaseSlotUI, IDropHandler
    {
        [SerializeField] private EquipmentSlotType slotType;
        public EquipmentSlotType SlotType => slotType;

        protected override void Awake()
        {
            base.Awake();
            OnBeginDragEvent += (s, e) => DragDropUIHandler.HandleBeginDragShared(s);
            OnPointerClickEvent += HandlePointerClick;
            OnEndDragEvent += (s, e) => HandleEndDrag(s, e);
        }

        public void OnEnable()
        {
            if (EquipmentDataManager.Instance != null)
            {
                EquipmentDataManager.Instance.OnEquipmentChanged += HandleEquipmentChanged;
            }
        }

        public void OnDisable()
        {
            if (EquipmentDataManager.Instance != null)
            {
                EquipmentDataManager.Instance.OnEquipmentChanged -= HandleEquipmentChanged;
            }
        }

        private void HandleEquipmentChanged(EquipmentSlotType changedSlotType, EquipmentData newItem, EquipmentData oldItem)
        {
            if (changedSlotType == this.slotType)
            {
                base.SetItem(newItem, newItem == null ? 0 : 1);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;

            BaseSlotUI sourceBaseSlot = eventData.pointerDrag.GetComponent<BaseSlotUI>();
            if (sourceBaseSlot == null || sourceBaseSlot.IsEmpty || sourceBaseSlot == this) return;

            if (sourceBaseSlot is InventorySlotUI sourceSlot && CanReceiveItem(sourceSlot.CurrentItem))
            {
                var (itemToEquip, count) = sourceSlot.GetItemInfo();

                if (InventoryManager.Instance.RemoveItemFromSlot(sourceSlot.SlotIndex, 1))
                {
                    if (!IsEmpty)
                    {
                        InventoryManager.Instance.AddItem(CurrentItem, 1);
                    }
                    SetItem(itemToEquip, 1);
                }
            }
        }

        public override void SetItem(ItemData newItem, int count = 1)
        {
            base.SetItem(newItem, count);
            UpdateEquipment(newItem);
        }

        public override bool CanReceiveItem(ItemData item)
        {
            if (item == null || !(item is EquipmentData equipment)) return false;

            switch (equipment.equipmentType)
            {
                case EquipmentType.Weapon:
                    return this.slotType == EquipmentSlotType.Weapon;
                case EquipmentType.Armor:
                    if (equipment is ArmorData armor)
                    {
                        switch (armor.armorType)
                        {
                            case ArmorType.Helmet: return this.slotType == EquipmentSlotType.Helmet;
                            case ArmorType.Armor: return this.slotType == EquipmentSlotType.Armor;
                            case ArmorType.Boots: return this.slotType == EquipmentSlotType.Boots;
                            case ArmorType.Accessory: return this.slotType == EquipmentSlotType.Accessory;
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }

        private void UpdateEquipment(ItemData itemToEquip)
        {
            if (EquipmentDataManager.Instance == null) return;
            EquipmentDataManager.Instance.SetEquipment(this.slotType, itemToEquip as EquipmentData);
        }

        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && !IsEmpty)
            {
                UnequipAndReturnToInventory();
            }
        }

        public void UnequipAndReturnToInventory()
        {
            if (IsEmpty) return;

            if (InventoryManager.Instance.AddItem(CurrentItem, 1))
            {
                ClearSlot();
            }
        }

        public override void ClearSlot()
        {
            ItemData itemToClear = CurrentItem;
            base.ClearSlot();
            if (itemToClear != null)
            {
                UpdateEquipment(null);
            }
        }
    }
}