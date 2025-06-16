using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using System;
using Nytherion.UI.Shop;
using Nytherion.UI.Inventory.Utils;

namespace Nytherion.UI.Inventory
{
    public class InventorySlotUI : BaseSlotUI
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

        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && !IsEmpty)
            {
                if (ShopUI.Instance != null && ShopUI.Instance.IsShopOpen())
                {
                    OnSellItemAction?.Invoke(this);
                }
                else
                {
                    Debug.Log($"[Inventory] Used Item: {currentItem.itemName}");
                }
            }
        }


        public override void ClearSlot()
        {
            base.ClearSlot();
        }
    }
}