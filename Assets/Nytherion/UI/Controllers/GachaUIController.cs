using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory;
using Nytherion.UI.Gacha;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.GamePlay.Engravings;
using UnityEngine.InputSystem;
using Nytherion.Core.Enums;
using VContainer;

namespace Nytherion.UI.Controllers
{
    public class GachaUIController : UIPanelBase
    {
        private GameSceneUIRefs gameSceneuiRefs;
        
        [Header("UI References")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button drawWeaponOnceButton;
        [SerializeField] private Button drawWeaponTenTimesButton;
        [SerializeField] private Button drawEngravingOnceButton;
        [SerializeField] private Button drawEngravingTenTimesButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resultCloseButton;
        [SerializeField] private Transform resultSlotParent;
        [SerializeField] private GameObject resultSlotPrefab;
        [SerializeField] private TextMeshProUGUI tokenCountText;

        private PlayerAction playerAction;
        private InventoryManager inventoryManager;
        private CurrencyManager currencyManager;
        private GachaManager gachaManager;
        private EventManager eventManager;

        [Inject]
        public void Construct(
            InventoryManager inventoryManager,
            CurrencyManager currencyManager,
            GachaManager gachaManager,
            EventManager eventManager,
            GameSceneUIRefs gameSceneuiRefs)
        {
            this.inventoryManager = inventoryManager;
            this.currencyManager = currencyManager;
            this.gachaManager = gachaManager;
            this.eventManager = eventManager;
            this.gameSceneuiRefs = gameSceneuiRefs;
            this.resultPanel = gameSceneuiRefs.GachaResultPanel;
            this.mainPanel = gameSceneuiRefs.GachaMainPanel;
            this.resultCloseButton = gameSceneuiRefs.ResultCloseButton;
            this.closeButton = gameSceneuiRefs.GachaCloseButton;
            this.drawWeaponOnceButton = gameSceneuiRefs.DrawWeaponOnceButton;
            this.drawWeaponTenTimesButton = gameSceneuiRefs.DrawWeaponTenTimesButton;
            this.drawEngravingOnceButton = gameSceneuiRefs.DrawEngravingOnceButton;
            this.drawEngravingTenTimesButton = gameSceneuiRefs.DrawEngravingTenTimesButton;
            this.resultSlotParent = gameSceneuiRefs.ResultSlotParent;
            this.resultSlotPrefab = gameSceneuiRefs.ResultSlotPrefab;
            this.tokenCountText = gameSceneuiRefs.TokenCountText;
            this.controlledCanvasGroup = gameSceneuiRefs.GachaCanvasGroup;
        }

        protected override void Awake()
        {
            base.Awake();
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
            if (currencyManager != null)
                currencyManager.onCurrencyChanged += UpdateTokenUI;

            playerAction = new PlayerAction();
            playerAction.GachaUI.Enable();
            playerAction.GachaUI.Close.performed += OnCloseInput;

            if (eventManager != null)
            {
                eventManager.OnInteraction += HandleInteraction;
            }
        }

        private void OnDisable()
        {
            if (currencyManager != null)
                currencyManager.onCurrencyChanged -= UpdateTokenUI;

            if (playerAction != null)
            {
                playerAction.GachaUI.Close.performed -= OnCloseInput;
                playerAction.GachaUI.Disable();
            }
            if (eventManager != null)
            {
                eventManager.OnInteraction -= HandleInteraction;
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
            if (isOpen && currencyManager != null)
            {
                UpdateTokenUI(CurrencyType.Token, currencyManager.GetCurrency(CurrencyType.Token));
            }

            if (isOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Draw(GachaType type, int count)
        {
            List<ScriptableObject> drawnItems = gachaManager.TryDrawItems(type, count);
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