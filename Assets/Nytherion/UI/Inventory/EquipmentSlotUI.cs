using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory.Utils;
using Nytherion.Core.Enums;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Inventory
{
    public class EquipmentSlotUI : BaseSlotUI, IDropHandler
    {
        [SerializeField] private EquipmentSlotType slotType;
        public EquipmentSlotType SlotType => slotType;

        private EquipmentDataManager equipmentDataManager;
        private InventoryDataManager inventoryDataManager;

        [Inject]
        public void Construct(EquipmentDataManager equipmentDataManager, InventoryDataManager inventoryDataManager)
        {
            this.equipmentDataManager = equipmentDataManager;
            this.inventoryDataManager = inventoryDataManager;
        }

        private void Start()
        {
            if (inventoryDataManager == null || equipmentDataManager == null)
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
                }
            }

            InitializeEquipmentState();
        }

        private void InitializeEquipmentState()
        {
            if (equipmentDataManager != null)
            {
                var currentEquipment = equipmentDataManager.GetEquipment(this.slotType);
                if (currentEquipment != null)
                {
                    base.SetItem(currentEquipment, 1);
                }
            }
        }

        public void RefreshFromLoadedData()
        {
            InitializeEquipmentState();
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
            base.HandleEndDrag(slot, eventData);
            
            DragDropUIHandler.HandleEndDragShared(slot, eventData);
            if (!eventData.pointerEnter)
            {
                if (iconImage != null) iconImage.enabled = true;
            }
            else
            {
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

                var currentEquipment = equipmentDataManager.GetEquipment(this.slotType);
                if (currentEquipment != null && base.IsEmpty)
                {
                    base.SetItem(currentEquipment, 1);
                }
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

                if (inventoryDataManager.RemoveItemFromSlot(sourceSlot.SlotIndex, 1))
                {
                    if (!IsEmpty)
                    {
                        inventoryDataManager.AddItem(CurrentItem, 1);
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

            EquipmentData equipment = itemToEquip as EquipmentData;
            if (equipment != null)
            {
                if (string.IsNullOrEmpty(equipment.instanceId))
                {
                    equipment.instanceId = System.Guid.NewGuid().ToString();
                }
            }

            equipmentDataManager.SetEquipment(this.slotType, equipment);
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

            if (inventoryDataManager.AddItem(CurrentItem, 1))
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