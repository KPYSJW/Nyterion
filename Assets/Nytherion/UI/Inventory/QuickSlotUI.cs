using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using System;
using InventoryUtils = Nytherion.UI.Inventory.Utils;
using UnityEngine.EventSystems;

namespace Nytherion.UI.Inventory
{
    public class QuickSlotUI : BaseSlotUI, IDropHandler
    {
        public event Action<ItemData, int> OnItemUsed;
        [SerializeField] private TMPro.TextMeshProUGUI keyLabelText;
        private Action<ItemData, int> onItemUsed;
        private IUseableItem useableItem;

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
            if (eventData.pointerDrag == null) return;
            BaseSlotUI sourceSlot = eventData.pointerDrag.GetComponent<BaseSlotUI>();
            if (sourceSlot == null || sourceSlot.IsEmpty) return;

            if (sourceSlot is InventorySlotUI inventorySourceSlot)
            {
                if (CanReceiveItem(inventorySourceSlot.CurrentItem))
                {
                    var (itemToMove, countToMove) = inventorySourceSlot.GetItemInfo();

                    if (InventoryManager.Instance.RemoveItemFromSlot(inventorySourceSlot.SlotIndex, countToMove))
                    {
                        if (!IsEmpty)
                        {
                            InventoryManager.Instance.AddItem(CurrentItem, CurrentCount);
                        }

                        SetItem(itemToMove, countToMove);
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

            Debug.Log($"[QuickSlot] {(item != null ? $"아이템 설정 완료: {item.itemName} x{count}" : "슬롯 비움")}");
        }

        public override void UseItem()
        {
            if (IsEmpty) return;

            if (InventoryManager.Instance.RemoveItem(currentItem, 1))
            {
                currentCount--;

                if (currentCount <= 0)
                {
                    ClearSlot();
                }
                else
                {
                    SetItem(currentItem, currentCount);
                }

                OnItemUsed?.Invoke(currentItem, 1);
            }
            else
            {
                Debug.Log("인벤토리에 해당 아이템이 없어 퀵슬롯에서 제거합니다.");
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