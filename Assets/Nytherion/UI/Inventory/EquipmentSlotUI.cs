using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory.Utils;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Enums;

namespace Nytherion.UI.Inventory
{
    public class EquipmentSlotUI : BaseSlotUI, IDropHandler
    {
        [SerializeField] private EquipmentSlotType slotType;
        public EquipmentSlotType SlotType { get { return slotType; } }
        protected override void Awake()
        {
            base.Awake();
            OnBeginDragEvent += (s, e) => DragDropUIHandler.HandleBeginDragShared(s);
            OnPointerClickEvent += HandlePointerClick;
            OnEndDragEvent += (s, e) => HandleEndDrag(s, e);
        }

        public void OnEnable()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnEquipmentChanged += HandleEquipmentChanged;
            }
        }
        public void OnDisable()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnEquipmentChanged -= HandleEquipmentChanged;
            }
        }
        private void HandleEquipmentChanged(EquipmentSlotType changedSlotType, EquipmentData newItem)
        {
            if (changedSlotType == this.slotType)
            {
                base.SetItem(newItem, newItem == null ? 0 : 1);
            }
        }
        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;

            BaseSlotUI sourceSlot = eventData.pointerDrag.GetComponent<BaseSlotUI>();

            if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot == this)
            {
                return;
            }

            if (sourceSlot is InventorySlotUI && CanReceiveItem(sourceSlot.CurrentItem))
            {
                ItemData itemToEquip = sourceSlot.CurrentItem;

                if (InventoryManager.Instance.RemoveItem(itemToEquip, 1))
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
            UpdatePlayerEquipment(newItem);
        }

        public override bool CanReceiveItem(ItemData item)
        {
            if (item == null || !(item is EquipmentData))
            {
                return false;
            }

            EquipmentData equipment = item as EquipmentData;

            switch (equipment.equipmentType)
            {
                case EquipmentType.Weapon:
                    return this.slotType == EquipmentSlotType.Weapon;

                case EquipmentType.Armor:
                    if (equipment is ArmorData armor)
                    {
                        switch (armor.armorType)
                        {
                            case ArmorType.Helmet:
                                return this.slotType == EquipmentSlotType.Helmet;
                            case ArmorType.Armor:
                                return this.slotType == EquipmentSlotType.Armor;
                            case ArmorType.Boots:
                                return this.slotType == EquipmentSlotType.Boots;
                            case ArmorType.Accessory:
                                return this.slotType == EquipmentSlotType.Accessory;
                            default:
                                return false;
                        }
                    }
                    return false;

                default:
                    return false;
            }
        }

        private void UpdatePlayerEquipment(ItemData itemToEquip)
        {
            if (PlayerManager.Instance == null)
            {
                Debug.LogError("[EquipmentSlotUI] PlayerManager.Instance is null. 장비를 장착/해제할 수 없습니다.");
                return;
            }

            PlayerManager.Instance.EquipItem(this.slotType, itemToEquip as EquipmentData);
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
            ItemData ItemToReturn = CurrentItem;
            PlayerManager.Instance.EquipItem(this.slotType, null);
            InventoryManager.Instance.AddItem(ItemToReturn, 1);
            ClearSlot();
        }

        public override void ClearSlot()
        {
            ItemData itemToClear = CurrentItem;
            base.ClearSlot();
            if (itemToClear != null)
            {
                UpdatePlayerEquipment(null);
            }
        }
        
    }
}