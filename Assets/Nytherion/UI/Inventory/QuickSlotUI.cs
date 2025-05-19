using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // LayoutRebuilder 사용을 위해 추가
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using System;
using EventSystem = UnityEngine.EventSystems.EventSystem; // 모호성 해결을 위한 별칭

namespace Nytherion.UI.Inventory
{
    public class QuickSlotUI : BaseSlotUI
    {
        public event Action<ItemData, int> OnItemUsed;
        [SerializeField] private TMPro.TextMeshProUGUI keyLabelText;
        private Action<ItemData, int> onItemUsed;

        protected override void Awake()
        {
            base.Awake();

            // 기본 키 라벨 설정 (필요에 따라 인스펙터에서 수정 가능)
            SetKeyLabel("");

            // 드래그 이벤트 핸들러 등록
            OnBeginDragEvent += HandleBeginDrag;
            OnEndDragEvent += HandleEndDrag;
        }

        private void HandleBeginDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            if (IsEmpty) return;
            Debug.Log($"[QuickSlotUI] Begin Drag {currentItem?.itemName}");

            // 드래그 아이콘 설정 및 표시
            if (DragItemIcon.Instance != null && currentItem != null)
            {
                DragItemIcon.Instance.SetIcon(iconImage.sprite);
                DragItemIcon.Instance.Show();
            }
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
            if (isSettingItem)
            {
                Debug.Log($"[QuickSlotUI] Already setting item, skipping...");
                return;
            }

            isSettingItem = true;
            Debug.Log($"[QuickSlotUI] SetItem called - Item: {item?.itemName ?? "null"}, Count: {count}");

            try
            {
                // ✅ 먼저 현재 아이템과 카운트 설정
                this.currentItem = item;
                this.currentCount = count;

                // 부모 클래스의 SetItem 호출 (UI 업데이트를 위해)
                base.SetItem(item, count, onUseCallback);

                // 콜백 설정 (null이 아닐 때만 업데이트)
                if (onUseCallback != null)
                {
                    this.onItemUsed = onUseCallback;
                    Debug.Log($"[QuickSlotUI] Callback set for {item?.itemName}");
                }

                // UI 강제 업데이트
                if (item != null)
                {
                    // 아이콘 설정
                    if (iconImage != null)
                    {
                        iconImage.sprite = item.icon;
                        iconImage.enabled = true;
                        Debug.Log($"[QuickSlotUI] Set icon: {item.icon?.name ?? "null"}");
                    }
                    else
                    {
                        Debug.LogError("[QuickSlotUI] iconImage is null!");
                    }

                    // 카운트 텍스트 설정
                    if (countText != null)
                    {
                        countText.text = item.isStackable && count > 1 ? count.ToString() : "";
                        Debug.Log($"[QuickSlotUI] Set count text: {countText.text}");
                    }
                    else
                    {
                        Debug.LogError("[QuickSlotUI] countText is null!");
                    }
                }
                else
                {
                    // 아이템이 null인 경우 UI 초기화
                    if (iconImage != null)
                    {
                        iconImage.enabled = false;
                        iconImage.sprite = null;
                    }
                    if (countText != null)
                    {
                        countText.text = "";
                    }
                }

                // 레이아웃 강제 갱신
                LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);

                // 디버그 로그
                if (item != null)
                {
                    Debug.Log($"[QuickSlotUI] SetItem: {item.itemName} x{count}");
                }
                else
                {
                    Debug.Log("[QuickSlotUI] Cleared slot");
                }
            }
            finally
            {
                isSettingItem = false;
            }
        }

        public override void UseItem()
        {
            if (IsEmpty) return;

            // 인벤토리에서 아이템 제거 시도
            if (InventoryManager.Instance != null && InventoryManager.Instance.RemoveItem(currentItem, 1))
            {
                // 인벤토리에서 아이템 제거 성공 시에만 퀵슬롯에서도 감소
                currentCount--;

                if (currentCount <= 0)
                {
                    // 수량이 0 이하가 되면 슬롯 클리어
                    ClearSlot();
                }
                else
                {
                    // 수량만 업데이트
                    SetItem(currentItem, currentCount);
                }

                // 인벤토리 UI 갱신
                InventoryManager.Instance.RequestSlotsUpdate();

                // 아이템 사용 이벤트 호출
                OnItemUsed?.Invoke(currentItem, 1);
            }
            else
            {
                // 인벤토리에 아이템이 없는 경우 슬롯 클리어
                Debug.Log("인벤토리에 해당 아이템이 없어 퀵슬롯에서 제거합니다.");
                ClearSlot();
            }
        }

        protected override void HandleEndDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            DragItemIcon.Instance?.Hide();
            if (IsEmpty) return;

            var results = new System.Collections.Generic.List<RaycastResult>();
            var pointerData = new PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = eventData.position
            };
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                // 인벤토리 슬롯 또는 다른 퀵슬롯 찾기
                var targetSlot = result.gameObject.GetComponentInParent<BaseSlotUI>();
                if (targetSlot == null || targetSlot == this) continue;

                // SlotTransferHelper를 사용하여 아이템 이동/교환 시도
                if (targetSlot is InventorySlotUI || targetSlot is QuickSlotUI)
                {
                    if (Utils.SlotTransferHelper.TryTransferItem(this, targetSlot))
                    {
                        Debug.Log($"[QuickSlotUI] Successfully transferred item to {targetSlot.GetType().Name}");
                    }
                    else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        // Shift 키를 누른 경우 아이템 교환 시도
                        Utils.SlotTransferHelper.TrySwapItems(this, targetSlot);
                    }
                    return;
                }

                // 드롭되지 않았을 경우 원래 아이템으로 복원
                SetItem(currentItem, currentCount, onItemUsed);
            }
        }

        public override void ClearSlot()
        {
            onItemUsed = null;
            base.ClearSlot();
        }
    }
}
