using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.GamePlay.Engravings;
using UnityEngine.InputSystem;
using Nytherion.Core.Enums;

namespace Nytherion.UI.Controllers
{
    public class GachaUIController : UIPanelBase
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

        protected override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            drawWeaponOnceButton.onClick.AddListener(() => Draw(GachaType.Weapon, 1));
            drawWeaponTenTimesButton.onClick.AddListener(() => Draw(GachaType.Weapon, 10));
            drawEngravingOnceButton.onClick.AddListener(() => Draw(GachaType.Engraving, 1));
            drawEngravingTenTimesButton.onClick.AddListener(() => Draw(GachaType.Engraving, 10));

            closeButton.onClick.AddListener(Close);
            resultCloseButton.onClick.AddListener(CloseResultPanel);

            resultPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.onCurrencyChanged += UpdateTokenUI;

            playerAction = new PlayerAction();
            playerAction.GachaUI.Enable();
            playerAction.GachaUI.Close.performed += OnCloseInput;

            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnInteraction += HandleInteraction;
            }
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.onCurrencyChanged -= UpdateTokenUI;

            if (playerAction != null)
            {
                playerAction.GachaUI.Close.performed -= OnCloseInput;
                playerAction.GachaUI.Disable();
            }
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnInteraction -= HandleInteraction;
            }
        }

        private void OnCloseInput(InputAction.CallbackContext context)
        {
            if (IsOpen)
            {
                Close();
            }
        }
        private void HandleInteraction(InteractableType type)
        {
            if (type == InteractableType.GachaNPC)
            {
                Toggle();
            }
        }
        public override void Close()
        {
            if (resultPanel != null && resultPanel.activeSelf)
            {
                CloseResultPanel();
            }
            base.Close();
        }


        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (isOpen && CurrencyManager.Instance != null)
            {
                UpdateTokenUI(CurrencyType.Token, CurrencyManager.Instance.GetCurrency(CurrencyType.Token));
            }

            if (isOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
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
            foreach (Transform child in resultSlotParent) Destroy(child.gameObject);

            foreach (ScriptableObject item in drawnItems)
            {
                GameObject slotGO = Instantiate(resultSlotPrefab, resultSlotParent);

                Transform iconTransform = slotGO.transform.Find("Icon");
                if (iconTransform == null)
                {
                    Debug.LogError("ResultSlotPrefab의 자식에서 'Icon' 오브젝트를 찾을 수 없습니다.");
                    continue;
                }

                Image itemIcon = iconTransform.GetComponent<Image>();
                if (itemIcon == null)
                {
                    Debug.LogError("'Icon' 오브젝트에서 Image 컴포넌트를 찾을 수 없습니다.");
                    continue;
                }

                if (item is ItemData itemData)
                {
                    itemIcon.sprite = itemData.icon;
                }
                else if (item is EngravingData engravingData)
                {
                    itemIcon.sprite = engravingData.Image;
                    GachaResultSlot resultSlot = slotGO.GetComponent<GachaResultSlot>();
                    if (resultSlot != null)
                    {
                        var tempEngravingBlock = new EngravingBlock(engravingData);
                        resultSlot.Setup(tempEngravingBlock);
                    }
                }

                iconTransform.SetAsLastSibling();
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