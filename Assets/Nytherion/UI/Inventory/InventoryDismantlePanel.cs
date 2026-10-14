using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory.Utils;
using Nytherion.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nytherion.UI.Inventory
{
    public class InventoryDismantlePanel : MonoBehaviour, IDropHandler
    {
        [Header("분해 보상")]
        [Tooltip("장비 기본 가치 대비 지급할 골드 비율입니다. 0.7은 70%입니다.")]
        [SerializeField, Min(0f)] private float valueRatio = 0.7f;
        [Header("UI 참조")]
        [SerializeField] private Transform slotParent;
        [SerializeField] private DismantleSlotUI slotTemplate;
        [SerializeField] private Button dismantleButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_Text expectedGoldText;
        [SerializeField] private GameObject emptyHint;

        private readonly List<DismantleSlotUI> slots = new List<DismantleSlotUI>();
        private InventoryDataManager inventory;
        private CurrencyDataManager currency;
        private AudioManager audioManager;
        private GameObject equipmentPanel;
        private GameObject statsPanel;
        private bool equipmentWasActive;
        private bool statsWasActive;
        public bool IsOpen { get; private set; }

        public void Initialize(InventoryDataManager inventory, CurrencyDataManager currency,
            GameObject equipmentPanel, GameObject statsPanel, AudioManager audioManager)
        {
            if (this.inventory != null) this.inventory.OnDataChanged -= OnInventoryChanged;
            this.inventory = inventory;
            this.currency = currency;
            this.equipmentPanel = equipmentPanel;
            this.statsPanel = statsPanel;
            this.audioManager = audioManager;
            dismantleButton.onClick.RemoveListener(Dismantle);
            dismantleButton.onClick.AddListener(Dismantle);
            resetButton.onClick.RemoveListener(ResetItems);
            resetButton.onClick.AddListener(ResetItems);
            inventory.OnDataChanged += OnInventoryChanged;
            slotTemplate.gameObject.SetActive(false);
            gameObject.SetActive(false);
            Refresh();
        }

        public void Toggle()
        {
            if (IsOpen) { Close(); return; }
            if (inventory == null || slotTemplate == null) return;
            equipmentWasActive = equipmentPanel != null && equipmentPanel.activeSelf;
            statsWasActive = statsPanel != null && statsPanel.activeSelf;
            if (equipmentPanel != null) equipmentPanel.SetActive(false);
            if (statsPanel != null) statsPanel.SetActive(false);
            IsOpen = true;
            gameObject.SetActive(true);
            Refresh();
            audioManager?.RequestUISound(UISoundType.Open);
            TooltipPanel.Instance?.HideTooltip();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            inventory?.ResetDismantle();
            gameObject.SetActive(false);
            if (equipmentPanel != null) equipmentPanel.SetActive(equipmentWasActive);
            if (statsPanel != null) statsPanel.SetActive(statsWasActive);
            audioManager?.RequestUISound(UISoundType.Close);
            TooltipPanel.Instance?.HideTooltip();
        }

        public bool TryAdd(InventorySlotUI source)
        {
            if (!IsOpen || source == null || !(source.CurrentItem is EquipmentData)) return false;
            bool success = inventory.StageForDismantle(source.SlotIndex, source.CurrentItem);
            if (success) TooltipPanel.Instance?.HideTooltip();
            return success;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!IsOpen || eventData.button != PointerEventData.InputButton.Left || eventData.pointerDrag == null) return;
            // 빈 패널/기존 분해 슬롯 어디에 놓아도 동일하게 추가하고 공용 슬롯 교환을 막는다.
            DragDropUIHandler.dropHandled = true;
            if (!TryAdd(eventData.pointerDrag.GetComponent<InventorySlotUI>()))
                audioManager?.RequestUISound(UISoundType.Error);
        }

        public void ResetItems()
        {
            inventory?.ResetDismantle();
            TooltipPanel.Instance?.HideTooltip();
            audioManager?.RequestUISound(UISoundType.Place);
        }

        public void Dismantle()
        {
            if (!IsOpen) return;
            bool success = inventory.DismantleEquipment(currency, valueRatio);
            audioManager?.RequestUISound(success ? UISoundType.ShopTransaction : UISoundType.Error);
            TooltipPanel.Instance?.HideTooltip();
        }

        private void OnInventoryChanged(InventoryChangeData change) => Refresh();

        private void Refresh()
        {
            if (inventory == null) return;
            var items = inventory.GetDismantleItems();
            while (slots.Count > items.Count)
            {
                var slot = slots[slots.Count - 1];
                slots.RemoveAt(slots.Count - 1);
                slot.gameObject.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying) { DestroyImmediate(slot.gameObject); continue; }
#endif
                Destroy(slot.gameObject);
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (i >= slots.Count)
                {
                    DismantleSlotUI slot = Instantiate(slotTemplate, slotParent);
                    slot.BindUIAudio(audioManager);
                    slot.gameObject.SetActive(true);
                    slots.Add(slot);
                }
                slots[i].SetItem(items[i].item, 1);
            }
            long gold = inventory.GetDismantleGold(valueRatio);
            expectedGoldText.text = $"예상 획득 골드: {gold:N0} G";
            dismantleButton.interactable = items.Count > 0 && currency != null && currency.IsInitialized &&
                gold <= int.MaxValue - (long)currency.GetGold();
            resetButton.interactable = items.Count > 0;
            emptyHint.SetActive(items.Count == 0);
        }

        private void OnDisable() { if (IsOpen) Close(); }
        private void OnDestroy()
        {
            Close();
            if (inventory != null) inventory.OnDataChanged -= OnInventoryChanged;
        }
    }
}
