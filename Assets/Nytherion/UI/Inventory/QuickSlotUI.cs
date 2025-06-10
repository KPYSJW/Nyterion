using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using System;
using EventSystem = UnityEngine.EventSystems.EventSystem;
using Nytherion.UI.Inventory.Utils;

namespace Nytherion.UI.Inventory
{
    public class QuickSlotUI : BaseSlotUI
    {
        public event Action<ItemData, int> OnItemUsed;
        [SerializeField] private TMPro.TextMeshProUGUI keyLabelText;
        private Action<ItemData, int> onItemUsed;
        private IUsableItem usableItem; // 사용 가능한 아이템 참조

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

            OnBeginDragEvent += (slot, eventData) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleBeginDragShared(slot);
            OnEndDragEvent += (slot, eventData) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleEndDragShared(slot, eventData); // allowShiftSwap defaults to true
        }


        public void SetKeyLabel(string label)
        {
            if (keyLabelText != null)
            {
                keyLabelText.text = label;
            }
        }

        public override void SetItem(ItemData item, int count, Action<ItemData, int> onUseCallback = null)
        {
            if (this.usableItem != null && this.usableItem is IDisposable disposable)
            {
                disposable.Dispose();
            }

            this.usableItem = item as IUsableItem;
            this.onItemUsed = onUseCallback;

            base.SetItem(item, count, (usedItem, usedCount) => 
            {
                if (this.usableItem != null)
                {
                    this.usableItem.Use();
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

            if (InventoryManager.Instance != null && InventoryManager.Instance.RemoveItem(currentItem, 1))
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

                InventoryManager.Instance.RequestSlotsUpdate();

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
