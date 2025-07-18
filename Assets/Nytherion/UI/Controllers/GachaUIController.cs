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
using Zenject;

namespace Nytherion.UI.Controllers
{
    public class GachaUIController : UIPanelBase
    {
        public static GachaUIController Instance { get; private set; }

        private GameObject mainPanel;
        private GameObject resultPanel;
        private Button drawWeaponOnceButton;
        private Button drawWeaponTenTimesButton;
        private Button drawEngravingOnceButton;
        private Button drawEngravingTenTimesButton;
        private Button closeButton;
        private Button resultCloseButton;
        private Transform resultSlotParent;
        private GameObject resultSlotPrefab;
        private TextMeshProUGUI tokenCountText;

        private PlayerAction playerAction;

        [Inject]
        public void Construct(
            [Inject(Id = "GachaCanvasGroup")] CanvasGroup controlledCanvasGroup,
            [Inject(Id = "GachaMainPanel")] GameObject mainPanel,
            [Inject(Id = "GachaResultPanel")] GameObject resultPanel,
            [Inject(Id = "DrawWeaponOnceButton")] Button drawWeaponOnceButton,
            [Inject(Id = "DrawWeaponTenTimesButton")] Button drawWeaponTenTimesButton,
            [Inject(Id = "DrawEngravingOnceButton")] Button drawEngravingOnceButton,
            [Inject(Id = "DrawEngravingTenTimesButton")] Button drawEngravingTenTimesButton,
            [Inject(Id = "GachaCloseButton")] Button closeButton,
            [Inject(Id = "ResultCloseButton")] Button resultCloseButton,
            [Inject(Id = "ResultSlotParent")] Transform resultSlotParent,
            [Inject(Id = "ResultSlotPrefab")] GameObject resultSlotPrefab,
            [Inject(Id = "TokenCountText")] TextMeshProUGUI tokenCountText)
        {
            this.controlledCanvasGroup = controlledCanvasGroup;
            this.mainPanel = mainPanel;
            this.resultPanel = resultPanel;
            this.drawWeaponOnceButton = drawWeaponOnceButton;
            this.drawWeaponTenTimesButton = drawWeaponTenTimesButton;
            this.drawEngravingOnceButton = drawEngravingOnceButton;
            this.drawEngravingTenTimesButton = drawEngravingTenTimesButton;
            this.closeButton = closeButton;
            this.resultCloseButton = resultCloseButton;
            this.resultSlotParent = resultSlotParent;
            this.resultSlotPrefab = resultSlotPrefab;
            this.tokenCountText = tokenCountText;
        }

        protected override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (drawWeaponOnceButton != null)
                drawWeaponOnceButton.onClick.AddListener(() => Draw(GachaType.Weapon, 1));

            if (drawWeaponTenTimesButton != null)
                drawWeaponTenTimesButton.onClick.AddListener(() => Draw(GachaType.Weapon, 10));

            if (drawEngravingOnceButton != null)
                drawEngravingOnceButton.onClick.AddListener(() => Draw(GachaType.Engraving, 1));

            if (drawEngravingTenTimesButton != null)
                drawEngravingTenTimesButton.onClick.AddListener(() => Draw(GachaType.Engraving, 10));

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (resultCloseButton != null)
                resultCloseButton.onClick.AddListener(CloseResultPanel);

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
            else
            {
                Debug.LogError("'resultPanel'이 GachaUIController에 할당되지 않았습니다.", this);
            }
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
            if (IsOpen && type != InteractableType.GachaNPC)
            {
                Close();
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