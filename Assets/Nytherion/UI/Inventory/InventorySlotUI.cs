using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Controllers;
using Nytherion.UI.Components;
using Nytherion.UI.Inventory.Utils;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        private InventoryUI inventoryUI;
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
        [Inject]
        public void BindInventoryUI(InventoryUI inventoryUI) => this.inventoryUI = inventoryUI;

        public override bool CanReceiveItem(ItemData item)
        {
            return inventoryDataManager?.InventoryModel?.IsDismantleReserved(SlotIndex) != true;
        }
        protected override void Awake()
        {
            base.Awake();
            PixelPerfectSlotFrame.Apply(slotBackgroundImage != null ? slotBackgroundImage : GetComponent<Image>());
            input = new ShopAction();
            OnBeginDragEvent += (s, e) => DragDropUIHandler.HandleBeginDragShared(s);
            OnEndDragEvent += (s, e) => DragDropUIHandler.HandleEndDragShared(s, e);
            OnPointerClickEvent += HandlePointerClick;
        }

        protected override void UpdateSlotBackground(ItemData item, bool hasItem)
        {
            base.UpdateSlotBackground(item, hasItem);
            // 빈 슬롯과 등급별 슬롯 모두 스프라이트 변경 직후 같은 픽셀 배율을 적용한다.
            PixelPerfectSlotFrame.Apply(slotBackgroundImage != null ? slotBackgroundImage : GetComponent<Image>());
        }

        protected override void UpdateVisuals(ItemData item, int count)
        {
            if (item != null && count > 0 && iconImage != null && iconImage.transform != transform)
                PixelPerfectItemIcon.Apply(iconImage, transform as RectTransform,
                    item is WeaponData weaponData ? weaponData.inventoryIconScale : 1f);
            base.UpdateVisuals(item, count);
        }

        protected override void ApplyIconLayout()
        {
            // 인벤토리는 원본 방향과 화면의 픽셀 격자를 유지한다.
            if (iconImage != null) iconImage.GetComponent<PixelPerfectItemIcon>()?.Refresh();
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
            if (!CanReceiveItem(null)) { DragDropUIHandler.dropHandled = true; return; }
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

                DragDropUIHandler.dropHandled = true;
                equipmentSourceSlot.TryMoveToInventory(this);
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
            if (inventoryUI != null && inventoryUI.IsDismantleOpen)
            {
                if (!inventoryUI.TryStageForDismantle(this)) PlayUISound(UISoundType.Error);
                return;
            }
            else if (CurrentItem is EquipmentData equipment)
            {
                EquipmentSlotType targetSlotType;

                if (equipment.equipmentType == EquipmentType.Weapon)
                {
                    targetSlotType = EquipmentSlotType.Weapon;
                }
                else
                {
                    return;
                }

                if (equipmentDataManager != null &&
                    equipmentDataManager.TryEquipFromInventory(targetSlotType, SlotIndex, equipment))
                {
                    PlayUISound(UISoundType.Place);
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
