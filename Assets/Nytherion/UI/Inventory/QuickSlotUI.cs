using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using System;
using InventoryUtils = Nytherion.UI.Inventory.Utils;
using UnityEngine.EventSystems;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Inventory
{
    public class QuickSlotUI : BaseSlotUI, IDropHandler
    {
        public int SlotIndex { get; private set; }
        public event Action<ItemData, int> OnItemUsed;
        [SerializeField] private TMPro.TextMeshProUGUI keyLabelText;
        private Action<ItemData, int> onItemUsed;
        private IUseableItem useableItem;
        private InventoryDataManager inventoryDataManager;
        private QuickSlotManager quickSlotManager;
        private ItemUsageManager itemUsageManager;

        [Inject]
        public void Construct(
            InventoryDataManager inventoryDataManager,
            QuickSlotManager quickSlotManager,
            ItemUsageManager itemUsageManager)
        {
            this.inventoryDataManager = inventoryDataManager;
            this.quickSlotManager = quickSlotManager;
            this.itemUsageManager = itemUsageManager;
        }

        private void Start()
        {
            if (inventoryDataManager == null || quickSlotManager == null || itemUsageManager == null)
            {
                var lifetimeScope = LifetimeScope.Find<GameSceneLifetimeScope>();
                if (lifetimeScope != null)
                {
                    if (inventoryDataManager == null && lifetimeScope.Container.TryResolve<InventoryDataManager>(out var invManager))
                    {
                        inventoryDataManager = invManager;
                    }

                    if (quickSlotManager == null && lifetimeScope.Container.TryResolve<QuickSlotManager>(out var quickManager))
                    {
                        quickSlotManager = quickManager;
                    }

                    if (itemUsageManager == null && lifetimeScope.Container.TryResolve<ItemUsageManager>(out var usageManager))
                    {
                        itemUsageManager = usageManager;
                    }
                }
            }
        }

        public void Initialize(int index)
        {
            this.SlotIndex = index;
        }
        protected override void Awake()
        {
            base.Awake();
            if (keyLabelText == null)
            {
                Debug.LogError("keyLabelText is not assigned in the inspector!", this);
            }
            else
            {
                keyLabelText.gameObject.SetActive(true);
            }

            OnBeginDragEvent += (slot, eventData) => InventoryUtils.DragDropUIHandler.HandleBeginDragShared(slot);
            OnEndDragEvent += (slot, eventData) => InventoryUtils.DragDropUIHandler.HandleEndDragShared(slot, eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (eventData.pointerDrag == null) return;
            BaseSlotUI sourceSlot = eventData.pointerDrag.GetComponent<BaseSlotUI>();
            if (sourceSlot == null || sourceSlot.IsEmpty) return;

            if (sourceSlot is InventorySlotUI inventorySourceSlot)
            {
                if (CanReceiveItem(inventorySourceSlot.CurrentItem))
                {
                    var (itemToMove, countToMove) = inventorySourceSlot.GetItemInfo();

                    if (inventoryDataManager.RemoveItemFromSlot(inventorySourceSlot.SlotIndex, countToMove))
                    {
                        if (!IsEmpty)
                        {
                            inventoryDataManager.AddItem(CurrentItem, CurrentCount);
                        }

                        SetItem(itemToMove, countToMove);

                        if (quickSlotManager != null)
                        {
                            quickSlotManager.UpdateSlotDataExternal(SlotIndex, itemToMove, countToMove);
                        }
                        
                    }
                    InventoryUtils.DragDropUIHandler.dropHandled = true;
                }
            }
            else if (sourceSlot is QuickSlotUI)
            {
                InventoryUtils.SlotTransferHelper.TransferItem(sourceSlot, this);
                InventoryUtils.DragDropUIHandler.dropHandled = true;
            }
        }

        public void SetKeyLabel(string label)
        {
            if (keyLabelText != null)
            {
                keyLabelText.text = label;
            }
        }

        public override bool CanReceiveItem(ItemData item)
        {
            return item is ConsumableData;
        }

        public override void SetItem(ItemData item, int count, Action<ItemData, int> onUseCallback = null)
        {
            if (this.useableItem != null && this.useableItem is IDisposable disposable)
            {
                disposable.Dispose();
            }

            this.useableItem = item as IUseableItem;
            this.onItemUsed = onUseCallback;

            base.SetItem(item, count, (usedItem, usedCount) =>
            {
                if (this.useableItem != null)
                {
                    this.useableItem.Use();
                }
                onItemUsed?.Invoke(usedItem, usedCount);
            });

            if (gameObject.activeInHierarchy && transform is RectTransform rectTransform)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }

            if (quickSlotManager != null)
            {
                quickSlotManager.UpdateSlotDataExternal(SlotIndex, item, count);
            }
        }

        public override void UseItem()
        {
            if (IsEmpty) return;

            if (quickSlotManager.ConsumeItemInQuickSlot(SlotIndex, 1))
            {
                itemUsageManager.UseConsumableItem(currentItem as ConsumableData);
                OnItemUsed?.Invoke(currentItem, 1);
            }
            else
            {
                ClearSlot();
            }
        }

        public override void ClearSlot()
        {
            this.onItemUsed = null;
            base.ClearSlot();
        }
    }
}