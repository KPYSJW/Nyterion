using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.RelicBoard
{
    public class RelicDismantlePanel : MonoBehaviour
    {
        [SerializeField] private Transform slotParent;
        [SerializeField] private Button dismantleButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_Dropdown maxRarityDropdown;
        [SerializeField] private Button bulkStageButton;
        [SerializeField] private TMP_Text expectedGoldText;
        [SerializeField] private GameObject emptyHint;
        [SerializeField] private GameObject[] replacedPanels;

        private readonly List<GameObject> slots = new List<GameObject>();
        private bool[] replacedPanelStates;
        private RelicManager manager;
        private CurrencyDataManager currency;
        private AudioManager audio;
        private RelicGridUI gridUI;
        private RelicTooltip tooltip;
        public bool IsOpen { get; private set; }

        public void Initialize(RelicManager manager, CurrencyDataManager currency, RelicGridUI gridUI,
            AudioManager audio, RelicTooltip tooltip)
        {
            if (this.manager != null) this.manager.OnRelicStateChanged -= Refresh;
            if (this.currency != null) this.currency.OnDataChanged -= OnCurrencyChanged;
            this.manager = manager;
            this.currency = currency;
            this.gridUI = gridUI;
            this.audio = audio;
            this.tooltip = tooltip;
            dismantleButton.onClick.RemoveListener(Dismantle);
            dismantleButton.onClick.AddListener(Dismantle);
            resetButton.onClick.RemoveListener(ResetItems);
            resetButton.onClick.AddListener(ResetItems);
            bulkStageButton.onClick.RemoveListener(StageByRarity);
            bulkStageButton.onClick.AddListener(StageByRarity);
            maxRarityDropdown.onValueChanged.RemoveListener(OnRarityChanged);
            maxRarityDropdown.onValueChanged.AddListener(OnRarityChanged);
            manager.OnRelicStateChanged += Refresh;
            currency.OnDataChanged += OnCurrencyChanged;
            if (!IsOpen) gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) { Close(); return; }
            if (manager == null || currency == null || gridUI == null) return;
            gridUI.CancelActiveDrag();
            gridUI.SetRelicCountHiddenForDismantle(true);
            replacedPanelStates = new bool[replacedPanels.Length];
            for (int i = 0; i < replacedPanels.Length; i++)
            {
                if (replacedPanels[i] == null) continue;
                replacedPanelStates[i] = replacedPanels[i].activeSelf;
                replacedPanels[i].SetActive(false);
            }
            IsOpen = true;
            gameObject.SetActive(true);
            Refresh();
            tooltip?.Hide();
            audio?.RequestUISound(UISoundType.Open);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            manager?.ResetDismantle();
            gameObject.SetActive(false);
            for (int i = 0; i < replacedPanels.Length; i++)
                if (replacedPanels[i] != null) replacedPanels[i].SetActive(replacedPanelStates[i]);
            gridUI?.SetRelicCountHiddenForDismantle(false);
            tooltip?.Hide();
            audio?.RequestUISound(UISoundType.Close);
        }

        public bool TryStage(RelicBlock block)
        {
            if (!IsOpen) return false;
            bool added = manager.StageForDismantle(block);
            tooltip?.Hide();
            audio?.RequestUISound(added ? UISoundType.Place : UISoundType.Error);
            return added;
        }

        public void ReturnItem(RelicBlock block)
        {
            if (!IsOpen) return;
            bool removed = manager.UnstageForDismantle(block);
            tooltip?.Hide();
            audio?.RequestUISound(removed ? UISoundType.Place : UISoundType.Error);
        }

        public void ResetItems()
        {
            if (!IsOpen) return;
            manager.ResetDismantle();
            tooltip?.Hide();
            audio?.RequestUISound(UISoundType.Place);
        }

        public void Dismantle()
        {
            if (!IsOpen) return;
            bool success = manager.DismantleRelics(currency);
            tooltip?.Hide();
            audio?.RequestUISound(success ? UISoundType.ShopTransaction : UISoundType.Error);
        }

        public void StageByRarity()
        {
            if (!IsOpen) return;
            int added = manager.StageForDismantleUpToRarity((Rarity)maxRarityDropdown.value);
            tooltip?.Hide();
            audio?.RequestUISound(added > 0 ? UISoundType.Place : UISoundType.Error);
        }

        private void OnRarityChanged(int _) => Refresh();
        private void OnCurrencyChanged(CurrencyChangeData _) => Refresh();

        private void Refresh()
        {
            if (!IsOpen || manager == null) return;
            foreach (GameObject slot in slots)
            {
                slot.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying) { DestroyImmediate(slot); continue; }
#endif
                Destroy(slot);
            }
            slots.Clear();
            List<RelicBlock> blocks = manager.GetDismantleBlocks();
            foreach (RelicBlock block in blocks) slots.Add(gridUI.CreateDismantleSlot(block, slotParent));
            long gold = manager.GetDismantleGold();
            expectedGoldText.text = $"예상 획득 골드: {gold:N0} G";
            dismantleButton.interactable = blocks.Count > 0 && currency.IsInitialized &&
                gold <= int.MaxValue - (long)currency.GetGold();
            resetButton.interactable = blocks.Count > 0;
            emptyHint.SetActive(blocks.Count == 0);
            Rarity maxRarity = (Rarity)maxRarityDropdown.value;
            bulkStageButton.interactable = manager.GetStorageBlocks().Any(block =>
                block?.SourceData != null && block.SourceData.IsRuntimeAvailable && !manager.IsStagedForDismantle(block) &&
                block.SourceData.rarity >= Rarity.Common && block.SourceData.rarity <= maxRarity);
        }

        private void OnDisable() => Close();
        private void OnDestroy()
        {
            Close();
            if (manager != null) manager.OnRelicStateChanged -= Refresh;
            if (currency != null) currency.OnDataChanged -= OnCurrencyChanged;
        }
    }
}
