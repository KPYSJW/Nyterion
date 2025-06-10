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
            // 슬롯이 비워질 때 추가 정리 작업이 필요하면 여기에 구현
        }
    }
}