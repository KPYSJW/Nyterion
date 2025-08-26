using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory.Utils;
using Nytherion.Core.Enums;
using Zenject;

namespace Nytherion.UI.Inventory
{
    public class EquipmentSlotUI : BaseSlotUI, IDropHandler
    {
        [SerializeField] private EquipmentSlotType slotType;
        public EquipmentSlotType SlotType => slotType;

        private EquipmentDataManager equipmentDataManager;
        private InventoryManager inventoryManager;

        [Inject]
        public void Construct(EquipmentDataManager equipmentDataManager, InventoryManager inventoryManager)
        {
            this.equipmentDataManager = equipmentDataManager;
            this.inventoryManager = inventoryManager;
        }

        protected override void Awake()
        {
            base.Awake();
            OnBeginDragEvent += (s, e) => DragDropUIHandler.HandleBeginDragShared(s);
            OnPointerClickEvent += HandlePointerClick;
            OnEndDragEvent += HandleEndDrag;
        }

        protected override void HandleEndDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            // 기본 드래그 종료 처리 호출
            base.HandleEndDrag(slot, eventData);
            
            DragDropUIHandler.HandleEndDragShared(slot, eventData);
            if (!eventData.pointerEnter)
            {
                // 드롭에 성공하지 못했다면, 아이콘을 다시 활성화
                if (iconImage != null) iconImage.enabled = true;
            }
            else
            {
                // 드롭에 성공했다면, 슬롯을 비움
                BaseSlotUI dropTarget = eventData.pointerEnter.GetComponent<BaseSlotUI>();
                if (dropTarget != null && dropTarget.CanReceiveItem(this.CurrentItem))
                {
                    ClearSlot();
                }
                else
                { 
                    if (iconImage != null) iconImage.enabled = true;
                }
            }
        }

        public void OnEnable()
        {
            if (equipmentDataManager != null)
            {
                equipmentDataManager.OnEquipmentChanged += HandleEquipmentChanged;
            }
        }

        public void OnDisable()
        {
            if (equipmentDataManager != null)
            {
                equipmentDataManager.OnEquipmentChanged -= HandleEquipmentChanged;
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
                if (itemToEquip == null) return;

                if (inventoryManager.RemoveItemFromSlot(sourceSlot.SlotIndex, 1))
                {
                    if (!IsEmpty)
                    {
                        inventoryManager.AddItem(CurrentItem, 1);
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
            if (equipmentDataManager == null) return;
            equipmentDataManager.SetEquipment(this.slotType, itemToEquip as EquipmentData);
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

            if (inventoryManager.AddItem(CurrentItem, 1))
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