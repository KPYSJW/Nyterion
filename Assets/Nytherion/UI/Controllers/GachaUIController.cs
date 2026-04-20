using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.UI.Gacha;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Engravings;
using UnityEngine.InputSystem;
using Nytherion.Core.Enums;
using VContainer;
using Nytherion.Core.Interfaces;

namespace Nytherion.UI.Controllers
{
    public class GachaUIController : UIPanelBase
    {
        private GameSceneUIRefs gameSceneuiRefs;

        [Header("Main Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject resultPanel;
        //[SerializeField] private Button drawWeaponOnceButton;
        //[SerializeField] private Button drawWeaponTenTimesButton;
        //[SerializeField] private Button drawEngravingOnceButton;
        //[SerializeField] private Button drawEngravingTenTimesButton;

        [Header("Gacha Sub Panels")]
        [SerializeField] private GameObject weaponSubPanel;
        [SerializeField] private GameObject engravingSubPanel;
        [SerializeField] private GameObject skillSubPanel;

        [Header("Tab Buttons")]
        [SerializeField] private Button weaponTabButton;
        [SerializeField] private Button engravingTabButton;
        [SerializeField] private Button skillTabButton;

        [Header("Action Buttons")]
        [SerializeField] private Button drawOnceBtton;
        [SerializeField] private Button drawTenBtton;

        [Header("Other UI")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resultCloseButton;
        [SerializeField] private Transform resultSlotParent;
        [SerializeField] private GameObject resultSlotPrefab;

        private PlayerAction playerAction;
        private InventoryManager inventoryManager;
        private CurrencyDataManager currencyDataManager;
        private GachaManager gachaManager;
        private EventManager eventManager;

        private GachaType currentSelectedType = GachaType.Weapon;

        [Inject]
        public void Construct(
            InventoryManager inventoryManager,
            CurrencyDataManager currencyDataManager,
            GachaManager gachaManager,
            EventManager eventManager,
            GameSceneUIRefs gameSceneuiRefs)
        {
            this.inventoryManager = inventoryManager;
            this.currencyDataManager = currencyDataManager;
            this.gachaManager = gachaManager;
            this.eventManager = eventManager;
            this.gameSceneuiRefs = gameSceneuiRefs;

            this.resultPanel = gameSceneuiRefs.GachaResultPanel;
            this.mainPanel = gameSceneuiRefs.GachaMainPanel;
            this.weaponSubPanel = gameSceneuiRefs.WeaponSubPanel;
            this.engravingSubPanel = gameSceneuiRefs.EngravingSubPanel;
            this.skillSubPanel = gameSceneuiRefs.SkillSubPanel;

            this.resultCloseButton = gameSceneuiRefs.ResultCloseButton;
            this.drawOnceBtton = gameSceneuiRefs.DrawOnceButton;
            this.drawTenBtton = gameSceneuiRefs.DrawTenButton;
            this.closeButton = gameSceneuiRefs.GachaCloseButton;
            this.weaponTabButton = gameSceneuiRefs.WeaponTabButton;
            this.engravingTabButton = gameSceneuiRefs.EngravingTabButton;
            this.skillTabButton = gameSceneuiRefs.SkillTabButton;

            this.resultSlotParent = gameSceneuiRefs.ResultSlotParent;
            this.resultSlotPrefab = gameSceneuiRefs.ResultSlotPrefab;
            this.controlledCanvasGroup = gameSceneuiRefs.GachaCanvasGroup;
        }

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            if (weaponTabButton != null) weaponTabButton.onClick.AddListener(() => SwitchTab(GachaType.Weapon));
            if (engravingTabButton != null) engravingTabButton.onClick.AddListener(() => SwitchTab(GachaType.Engraving));
            if (skillTabButton != null) skillTabButton.onClick.AddListener(() => SwitchTab(GachaType.Skill));

            if (drawOnceBtton != null)
                drawOnceBtton.onClick.AddListener(() => Draw(currentSelectedType, 1));

            if (drawTenBtton != null)
                drawTenBtton.onClick.AddListener(() => Draw(currentSelectedType, 10));

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (resultCloseButton != null)
                resultCloseButton.onClick.AddListener(CloseResultPanel);

            if (resultPanel != null)
                resultPanel.SetActive(false);
            else
                Debug.LogError("'resultPanel'이 GachaUIController에 할당되지 않았습니다.", this);

            SwitchTab(GachaType.Weapon);
        }
        public void SwitchTab(GachaType type)
        {
            currentSelectedType = type;

            if (weaponSubPanel != null) weaponSubPanel.SetActive(type == GachaType.Weapon);
            if (engravingSubPanel != null) engravingSubPanel.SetActive(type == GachaType.Engraving);
            if (skillSubPanel != null) skillSubPanel.SetActive(type == GachaType.Skill);

            SetButtonAlpha(weaponTabButton, type == GachaType.Weapon ? 1f : 0.5f);
            SetButtonAlpha(engravingTabButton, type == GachaType.Engraving ? 1f : 0.5f);
            SetButtonAlpha(skillTabButton, type == GachaType.Skill ? 1f : 0.5f);
        }
        private void SetButtonAlpha(Button btn, float alpha)
        {
            if (btn == null) return;
            var colors = btn.colors;
            var normalColor = colors.normalColor;
            normalColor.a = alpha;
            colors.normalColor = normalColor;
            btn.colors = colors;
        }
        private void OnEnable()
        {
            if (currencyDataManager != null) currencyDataManager.OnDataChanged += UpdateTokenUI;

            playerAction = new PlayerAction();
            playerAction.GachaUI.Enable();
            playerAction.GachaUI.Close.performed += OnCloseInput;

            if (eventManager != null) eventManager.OnInteraction += HandleInteraction;
        }
        private void OnDisable()
        {
            if (currencyDataManager != null) currencyDataManager.OnDataChanged -= UpdateTokenUI;

            if (playerAction != null)
            {
                playerAction.GachaUI.Close.performed -= OnCloseInput;
                playerAction.GachaUI.Disable();
            }
            if (eventManager != null) eventManager.OnInteraction -= HandleInteraction;
        }

        private void OnCloseInput(InputAction.CallbackContext context)
        {
            if (IsOpen) Close();
        }
        private void HandleInteraction(InteractableType type)
        {
            if (IsOpen && type != InteractableType.GachaNPC) Close();
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
            if (isOpen && currencyDataManager != null)
            {
                UpdateTokenUI(new CurrencyChangeData
                {
                    currencyType = CurrencyType.Token,
                    newAmount = currencyDataManager.GetCurrency(CurrencyType.Token)
                });
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
            if (mainPanel != null) mainPanel.SetActive(false);
            resultPanel.SetActive(true);

            foreach (Transform child in resultSlotParent) Destroy(child.gameObject);

            foreach (ScriptableObject item in drawnItems)
            {
                GameObject slotGO = Instantiate(resultSlotPrefab, resultSlotParent);

                Transform iconTransform = slotGO.transform.Find("Icon");
                if (iconTransform == null) continue;

                Image itemIcon = iconTransform.GetComponent<Image>();
                if (itemIcon == null) continue;

                GachaResultSlot resultSlot = slotGO.GetComponent<GachaResultSlot>();

                if (item is ItemData itemData)
                {
                    itemIcon.sprite = itemData.icon;
                    if (resultSlot != null) resultSlot.Setup(itemData);
                }
                else if (item is EngravingData engravingData)
                {
                    itemIcon.sprite = engravingData.Image;
                    if (resultSlot != null)
                    {
                        var tempEngravingBlock = new EngravingBlock(engravingData);
                        resultSlot.Setup(tempEngravingBlock);
                    }
                }
                else if (item is SkillData skillData)
                {
                    itemIcon.sprite = skillData.icon;
                }

                iconTransform.SetAsLastSibling();
            }
        }
        private void CloseResultPanel()
        {
            resultPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        private void UpdateTokenUI(CurrencyChangeData data)
        {
            if (data.currencyType == CurrencyType.Token)
            {
                // UI 갱신 로직
            }
        }
    }
}