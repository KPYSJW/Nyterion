using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Gacha
{
    public class GachaUIController : MonoBehaviour
    {
        public static GachaUIController Instance { get; private set; }

        [Header("UI Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject resultPanel;

        [Header("Buttons")]
        [SerializeField] private Button drawWeaponOnceButton;
        [SerializeField] private Button drawWeaponTenTimesButton;
        [SerializeField] private Button drawEngravingOnceButton;
        [SerializeField] private Button drawEngravingTenTimesButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resultCloseButton;

        [Header("Result Panel Settings")]
        [SerializeField] private Transform resultSlotParent;
        [SerializeField] private GameObject resultSlotPrefab;

        [Header("Currency Display")]
        [SerializeField] private TextMeshProUGUI tokenCountText;

        private PlayerAction playerAction;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            drawWeaponOnceButton.onClick.AddListener(() => Draw(GachaType.Weapon, 1));
            drawWeaponTenTimesButton.onClick.AddListener(() => Draw(GachaType.Weapon, 10));
            drawEngravingOnceButton.onClick.AddListener(() => Draw(GachaType.Engraving, 1));
            drawEngravingTenTimesButton.onClick.AddListener(() => Draw(GachaType.Engraving, 10));

            closeButton.onClick.AddListener(CloseUI);
            resultCloseButton.onClick.AddListener(CloseResultPanel);

            mainPanel.SetActive(false);
            resultPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.onCurrencyChanged += UpdateTokenUI;

            playerAction = new PlayerAction();
            playerAction.GachaUI.Close.performed += _ => CloseUI();
            playerAction.GachaUI.ToggleUI.performed += _ => ToggleUI();

        }
        private void OnDisable()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.onCurrencyChanged -= UpdateTokenUI;

            if (playerAction != null)
            {
                playerAction.GachaUI.Close.performed -= _ => CloseUI();
                playerAction.GachaUI.ToggleUI.performed -= _ => ToggleUI();
            }
        }

        public void OnClick_DrawWeaponOnce() { Draw(GachaType.Weapon, 1); }
        public void OnClick_DrawWeaponTenTimes() { Draw(GachaType.Weapon, 10); }
        public void OnClick_DrawEngravingOnce() { Draw(GachaType.Engraving, 1); }
        public void OnClick_DrawEngravingTenTimes() { Draw(GachaType.Engraving, 10); }
        public void OnClick_CloseUI() { CloseUI(); }
        public void OnClick_CloseResultPanel() { CloseResultPanel(); }
        public void ToggleUI()
        {
            bool isActive = !mainPanel.activeSelf;
            if (isActive) OpenUI();
            else CloseUI();
        }
        public void OpenUI()
        {
            mainPanel.SetActive(true);
            if (CurrencyManager.Instance != null)
                UpdateTokenUI(CurrencyType.Token, CurrencyManager.Instance.GetCurrency(CurrencyType.Token));
        }
        public void CloseUI()
        {
            mainPanel.SetActive(false);
        }
        private void Draw(GachaType type, int count)
        {
            List<ScriptableObject> drawnItems = GachaManager.Instance.TryDrawItems(type, count);
            if (drawnItems != null && drawnItems.Count > 0)
            {
                ShowResultPanel(drawnItems);
            }
        }
        private void ShowResultPanel(List<ScriptableObject> drawnItems)
        {
            resultPanel.SetActive(true);

            foreach (Transform child in resultSlotParent)
            {
                Destroy(child.gameObject);
            }
            foreach (ScriptableObject item in drawnItems)
            {
                GameObject slotGO = Instantiate(resultSlotPrefab, resultSlotParent);
                Image itemIcon = slotGO.GetComponent<Image>();
                ItemData itemData = item as ItemData;
                if (itemData != null)
                {
                    itemIcon.sprite = itemData.icon;
                }
            }
        }
        private void CloseResultPanel()
        {
            resultPanel.SetActive(false);
        }
        private void UpdateTokenUI(CurrencyType type, int amount)
        {
            if (type == CurrencyType.Token)
            {
                tokenCountText.text = amount.ToString();
            }
        }
    }
}