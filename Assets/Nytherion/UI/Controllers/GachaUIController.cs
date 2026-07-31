using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.UI.Gacha;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Relics;
using UnityEngine.InputSystem;
using Nytherion.Core.Enums;
using VContainer;
using Nytherion.Core.Interfaces;
using TMPro;

namespace Nytherion.UI.Controllers
{
    public class GachaUIController : UIPanelBase
    {
        private GameSceneUIRefs gameSceneuiRefs;

        [Header("Main Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject relicResultPanel;

        [Header("Gacha Sub Panels & Animators")]
        [SerializeField] private GameObject relicSubPanel;
        [SerializeField] private Animator relicAnimator;

        [Header("Gacha Type Indicator Text")]
        [SerializeField] private TextMeshProUGUI gachaTypeTitleText;

        [Header("Action Buttons")]
        [SerializeField] private Button drawOnceBtton;
        [SerializeField] private Button drawTenBtton;

        [Header("Other UI")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button relicResultCloseButton;
        [SerializeField] private Transform relicResultSlotParent;
        [SerializeField] private GameObject resultSlotPrefab;

        private PlayerAction playerAction;
        private InventoryManager inventoryManager;
        private CurrencyDataManager currencyDataManager;
        private GachaManager gachaManager;
        private EventManager eventManager;

        private GachaType currentSelectedType = GachaType.Relic;
        private bool isDrawing = false;
        private static readonly int DrawTriggerHash = Animator.StringToHash("Draw");

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

            this.relicResultPanel = gameSceneuiRefs.RelicResultPanel;
            this.mainPanel = gameSceneuiRefs.GachaMainPanel;
            this.relicSubPanel = gameSceneuiRefs.RelicSubPanel;

            this.relicResultCloseButton = gameSceneuiRefs.RelicResultCloseButton;
            this.drawOnceBtton = gameSceneuiRefs.DrawOnceButton;
            this.drawTenBtton = gameSceneuiRefs.DrawTenButton;
            this.closeButton = gameSceneuiRefs.GachaCloseButton;

            this.gachaTypeTitleText = gameSceneuiRefs.GachaTypeTitleText;

            this.relicResultSlotParent = gameSceneuiRefs.RelicResultSlotParent;
            this.resultSlotPrefab = gameSceneuiRefs.ResultSlotPrefab;
            this.controlledCanvasGroup = gameSceneuiRefs.GachaCanvasGroup;

            if (relicSubPanel != null && relicAnimator == null)
            {
                relicAnimator = relicSubPanel.GetComponentInChildren<Animator>();
            }
        }

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            if (drawOnceBtton != null)
                drawOnceBtton.onClick.AddListener(() => Draw(currentSelectedType, 1));

            if (drawTenBtton != null)
                drawTenBtton.onClick.AddListener(() => Draw(currentSelectedType, 10));

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (relicResultCloseButton != null)
                relicResultCloseButton.onClick.AddListener(CloseResultPanel);

            if (relicResultPanel != null)
                relicResultPanel.SetActive(false);
            else
                Debug.LogError("'relicResultPanel'이 GachaUIController에 할당되지 않았습니다.", this);

            SwitchTab(GachaType.Relic);
        }

        public void ToggleGachaType()
        {
            // 더 이상 무기/유물 탭 전환을 지원하지 않고 유물 가챠 전용으로 유지합니다.
        }

        public void SwitchTab(GachaType type)
        {
            if (isDrawing) return;

            currentSelectedType = GachaType.Relic;

            ResetAnimators();

            if (relicSubPanel != null) relicSubPanel.SetActive(true);

            if (gachaTypeTitleText != null)
            {
                gachaTypeTitleText.text = "Relic";
            }
        }

        private void SetButtonAlpha(Button btn, float alpha)
        {
            if (btn == null) return;
            ColorBlock colors = btn.colors;
            Color normalColor = colors.normalColor;
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
            if (IsOpen && !isDrawing) Close();
        }

        private void HandleInteraction(InteractableType type)
        {
            if (IsOpen && type != InteractableType.GachaNPC && !isDrawing) Close();
        }

        public override void Close()
        {
            if (isDrawing) return;

            if (relicResultPanel != null && relicResultPanel.activeSelf)
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

        private List<ScriptableObject> pendingDrawnItems;

        private void Draw(GachaType type, int count)
        {
            if (isDrawing) return;

            // 1. 가챠 추첨 시도 (유물 가챠 실행)
            List<ScriptableObject> drawnItems = gachaManager.TryDrawItems(GachaType.Relic, count);
            if (drawnItems == null || drawnItems.Count == 0)
            {
                return;
            }

            pendingDrawnItems = drawnItems;
            ResetAnimators();

            // 2. 가챠 연출 발동 및 이벤트 수신 시퀀스 실행
            StartCoroutine(Co_PlayGachaSequence(GachaType.Relic));
        }

        [Header("Gacha Animation Settings")]
        [Tooltip("가챠 연출 대기 시간 (초 단위, 원하시는 연출 시간에 맞춰 자유롭게 조절 가능)")]
        [SerializeField] private float gachaAnimDuration = 0.8f;

        private System.Collections.IEnumerator Co_PlayGachaSequence(GachaType type)
        {
            isDrawing = true;
            SetButtonsInteractable(false);

            Animator targetAnimator = relicAnimator;
            if (targetAnimator == null && relicSubPanel != null)
            {
                targetAnimator = relicSubPanel.GetComponentInChildren<Animator>();
            }

            if (targetAnimator != null && targetAnimator.gameObject.activeInHierarchy)
            {
                targetAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                targetAnimator.enabled = true;
                targetAnimator.SetTrigger(DrawTriggerHash);
            }

            yield return new WaitForSecondsRealtime(gachaAnimDuration);

            if (isDrawing)
            {
                OnGachaAnimationFinished();
            }
        }

        public void OnGachaAnimationFinished()
        {
            if (!isDrawing) return;

            StopAllCoroutines();

            if (pendingDrawnItems != null && pendingDrawnItems.Count > 0)
            {
                ShowResultPanel(pendingDrawnItems, GachaType.Relic);
                pendingDrawnItems = null;
            }

            SetButtonsInteractable(true);
            isDrawing = false;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (drawOnceBtton != null) drawOnceBtton.interactable = interactable;
            if (drawTenBtton != null) drawTenBtton.interactable = interactable;
            if (closeButton != null) closeButton.interactable = interactable;
        }

        private void ShowResultPanel(List<ScriptableObject> drawnItems, GachaType type)
        {
            ResetAnimators();

            if (mainPanel != null) mainPanel.SetActive(false);

            if (relicResultPanel != null) relicResultPanel.SetActive(true);

            if (relicResultSlotParent != null && resultSlotPrefab != null)
            {
                foreach (Transform child in relicResultSlotParent)
                {
                    ObjectPoolManager.Instance.ReturnToPool(resultSlotPrefab.name, child.gameObject);
                }

                foreach (ScriptableObject item in drawnItems)
                {
                    GameObject slotGO = ObjectPoolManager.Instance.SpawnFromPool(resultSlotPrefab, Vector3.zero, Quaternion.identity);
                    slotGO.transform.SetParent(relicResultSlotParent, false);

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
                    else if (item is RelicData relicData)
                    {
                        itemIcon.sprite = relicData.Image;
                        if (resultSlot != null)
                        {
                            RelicBlock tempRelicBlock = new RelicBlock(relicData);
                            resultSlot.Setup(tempRelicBlock);
                        }
                    }
                    else if (item is SkillData skillData)
                    {
                        itemIcon.sprite = skillData.icon;
                    }

                    iconTransform.SetAsLastSibling();
                }
            }

        }

        private void CloseResultPanel()
        {
            if (relicResultPanel != null) relicResultPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);

            ResetAnimators();
        }

        private void ResetAnimators()
        {
            ResetAnimator(relicAnimator, relicSubPanel);
        }

        private void ResetAnimator(Animator anim, GameObject subPanel)
        {
            if (anim == null && subPanel != null)
            {
                anim = subPanel.GetComponentInChildren<Animator>();
            }

            if (anim != null && anim.gameObject.activeInHierarchy)
            {
                anim.Rebind();
                anim.Update(0f);
            }
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
