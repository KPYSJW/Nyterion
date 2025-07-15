using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Core.Managers;
using System;
using Nytherion.UI.Inventory.Utils;
using Nytherion.UI.Controllers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.UI.Inventory
{
    public class InventorySlotUI : BaseSlotUI, IDropHandler
    {
        public int SlotIndex { get; private set; }

        public event Action<BaseSlotUI> OnSellItemAction;
        public int CurrentAmount => IsEmpty ? 0 : InventoryManager.Instance.GetItemCount(CurrentItem);
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

            if (sourceSlot is EquipmentSlotUI equipmentSourceSlot)
            {
                if (this.IsEmpty)
                {
                    ItemData itemToMove = equipmentSourceSlot.CurrentItem;
                    PlayerManager.Instance.EquipItem(equipmentSourceSlot.SlotType, null);
                    InventoryManager.Instance.AddItemWithoutNotify(itemToMove, 1);
                    this.SetItem(itemToMove, 1);
                    equipmentSourceSlot.ClearSlot();
                }
                else
                {
                    equipmentSourceSlot.SetItem(equipmentSourceSlot.CurrentItem, equipmentSourceSlot.CurrentCount);
                }
                return;
            }
            if (sourceSlot != this)
            {
                SlotTransferHelper.TransferItem(sourceSlot, this);
            }
        }
        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (ItemOnCursor.IsHoldingItem)
            {
                var (heldItem, heldCount) = ItemOnCursor.GetAndClear();
                if (IsEmpty || (CurrentItem.ID == heldItem.ID && CurrentItem.isStackable))
                {
                    int newCount = IsEmpty ? 0 : CurrentCount;
                    int total = newCount + heldCount;
                    int maxStack = heldItem.maxStack;

                    if (total <= maxStack)
                    {
                        SetItem(heldItem, total);
                    }
                    else
                    {
                        SetItem(heldItem, maxStack);
                        ItemOnCursor.Set(heldItem, total - maxStack);
                    }
                }
                else
                {
                    var myItem = CurrentItem;
                    var myCount = CurrentCount;
                    SetItem(heldItem, heldCount);
                    ItemOnCursor.Set(myItem, myCount);
                }
            }
            else
            {
                if (IsEmpty) return;

                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    bool ctrlPressed = InputManager.Instance.IsControlPressed;
                    bool shiftPressed = InputManager.Instance.IsShiftPressed;

                    if (CurrentCount > 1 && (ctrlPressed || shiftPressed))
                    {
                        int amountToPickUp = shiftPressed ? Mathf.CeilToInt(CurrentCount / 2.0f) : 1;

                        var itemToHold = CurrentItem;

                        DecreaseCount(amountToPickUp);

                        ItemOnCursor.Set(itemToHold, amountToPickUp);
                    }
                    else
                    {
                        if (ShopUI.Instance != null && ShopUI.Instance.IsOpen)
                        {
                            OnSellItemAction?.Invoke(this);
                        }
                        else
                        {
                            Debug.Log($"[Inventory] Used Item: {currentItem.itemName}");
                        }
                    }
                }
                else if (eventData.button == PointerEventData.InputButton.Left)
                {
                    var itemToHold = CurrentItem;
                    var countToHold = CurrentCount;
                    ClearSlot();
                    ItemOnCursor.Set(itemToHold, countToHold);
                }
            }
        }


        public override void ClearSlot()
        {
            base.ClearSlot();
        }
    }
}