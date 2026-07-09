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
        [SerializeField] private GameObject weaponResultPanel;
        [SerializeField] private GameObject relicResultPanel;

        [Header("Gacha Sub Panels & Animators")]
        [SerializeField] private GameObject weaponSubPanel;
        [SerializeField] private GameObject relicSubPanel;
        [SerializeField] private Animator weaponAnimator;
        [SerializeField] private Animator relicAnimator;

        [Header("Gacha Type Indicator Text")]
        [SerializeField] private TextMeshProUGUI gachaTypeTitleText;

        [Header("Navigation Buttons")]
        [SerializeField] private Button prevGachaButton;
        [SerializeField] private Button nextGachaButton;

        [Header("Action Buttons")]
        [SerializeField] private Button drawOnceBtton;
        [SerializeField] private Button drawTenBtton;

        [Header("Other UI")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button weaponResultCloseButton;
        [SerializeField] private Button relicResultCloseButton;
        [SerializeField] private Transform weaponResultSlotParent;
        [SerializeField] private Transform relicResultSlotParent;
        [SerializeField] private GameObject resultSlotPrefab;

        private PlayerAction playerAction;
        private InventoryManager inventoryManager;
        private CurrencyDataManager currencyDataManager;
        private GachaManager gachaManager;
        private EventManager eventManager;

        private GachaType currentSelectedType = GachaType.Weapon;
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

            this.weaponResultPanel = gameSceneuiRefs.WeaponResultPanel;
            this.relicResultPanel = gameSceneuiRefs.RelicResultPanel;
            this.mainPanel = gameSceneuiRefs.GachaMainPanel;
            this.weaponSubPanel = gameSceneuiRefs.WeaponSubPanel;
            this.relicSubPanel = gameSceneuiRefs.RelicSubPanel;

            this.weaponResultCloseButton = gameSceneuiRefs.WeaponResultCloseButton;
            this.relicResultCloseButton = gameSceneuiRefs.RelicResultCloseButton;
            this.drawOnceBtton = gameSceneuiRefs.DrawOnceButton;
            this.drawTenBtton = gameSceneuiRefs.DrawTenButton;
            this.closeButton = gameSceneuiRefs.GachaCloseButton;

            this.gachaTypeTitleText = gameSceneuiRefs.GachaTypeTitleText;
            this.prevGachaButton = gameSceneuiRefs.PrevGachaButton;
            this.nextGachaButton = gameSceneuiRefs.NextGachaButton;

            this.weaponResultSlotParent = gameSceneuiRefs.WeaponResultSlotParent;
            this.relicResultSlotParent = gameSceneuiRefs.RelicResultSlotParent;
            this.resultSlotPrefab = gameSceneuiRefs.ResultSlotPrefab;
            this.controlledCanvasGroup = gameSceneuiRefs.GachaCanvasGroup;

            // 서브 패널 자식 오브젝트에서 Animator 컴포넌트 자동 탐색 (보완)
            if (weaponSubPanel != null && weaponAnimator == null)
            {
                weaponAnimator = weaponSubPanel.GetComponentInChildren<Animator>();
            }
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
            if (prevGachaButton != null) prevGachaButton.onClick.AddListener(ToggleGachaType);
            if (nextGachaButton != null) nextGachaButton.onClick.AddListener(ToggleGachaType);

            if (drawOnceBtton != null)
                drawOnceBtton.onClick.AddListener(() => Draw(currentSelectedType, 1));

            if (drawTenBtton != null)
                drawTenBtton.onClick.AddListener(() => Draw(currentSelectedType, 10));

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (weaponResultCloseButton != null)
                weaponResultCloseButton.onClick.AddListener(CloseResultPanel);

            if (relicResultCloseButton != null)
                relicResultCloseButton.onClick.AddListener(CloseResultPanel);

            if (weaponResultPanel != null)
                weaponResultPanel.SetActive(false);
            else
                Debug.LogError("'weaponResultPanel'이 GachaUIController에 할당되지 않았습니다.", this);

            if (relicResultPanel != null)
                relicResultPanel.SetActive(false);
            else
                Debug.LogError("'relicResultPanel'이 GachaUIController에 할당되지 않았습니다.", this);

            SwitchTab(GachaType.Weapon);
        }

        public void ToggleGachaType()
        {
            if (isDrawing) return;

            GachaType nextType = (currentSelectedType == GachaType.Weapon) ? GachaType.Relic : GachaType.Weapon;
            SwitchTab(nextType);
        }

        public void SwitchTab(GachaType type)
        {
            if (isDrawing) return;

            currentSelectedType = type;

            ResetAnimators();

            if (weaponSubPanel != null) weaponSubPanel.SetActive(type == GachaType.Weapon);
            if (relicSubPanel != null) relicSubPanel.SetActive(type == GachaType.Relic);

            if (gachaTypeTitleText != null)
            {
                gachaTypeTitleText.text = (type == GachaType.Weapon) ? "Weapon" : "Relic";
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

            if ((weaponResultPanel != null && weaponResultPanel.activeSelf) || (relicResultPanel != null && relicResultPanel.activeSelf))
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

            // 1. 가챠 추첨 시도 (토큰 부족 or 인벤토리 조건 검증)
            List<ScriptableObject> drawnItems = gachaManager.TryDrawItems(type, count);
            if (drawnItems == null || drawnItems.Count == 0)
            {
                // 조건 미충족 시 연출 없이 종료
                return;
            }

            pendingDrawnItems = drawnItems;
            ResetAnimators();

            // 2. 가챠 연출 발동 및 이벤트 수신 시퀀스 실행
            StartCoroutine(Co_PlayGachaSequence(type));
        }

        [Header("Gacha Animation Settings")]
        [Tooltip("가챠 연출 대기 시간 (초 단위, 원하시는 연출 시간에 맞춰 자유롭게 조절 가능)")]
        [SerializeField] private float gachaAnimDuration = 0.8f;

        private System.Collections.IEnumerator Co_PlayGachaSequence(GachaType type)
        {
            isDrawing = true;
            SetButtonsInteractable(false);

            Animator targetAnimator = (type == GachaType.Weapon) ? weaponAnimator : relicAnimator;
            if (targetAnimator == null)
            {
                GameObject subPanel = (type == GachaType.Weapon) ? weaponSubPanel : relicSubPanel;
                if (subPanel != null)
                {
                    targetAnimator = subPanel.GetComponentInChildren<Animator>();
                }
            }

            if (targetAnimator != null && targetAnimator.gameObject.activeInHierarchy)
            {
                targetAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                targetAnimator.enabled = true;
                targetAnimator.SetTrigger(DrawTriggerHash);
            }

            // 설정된 가챠 연출 대기 시간만큼 정확히 대기 후 결과 창 출력
            yield return new WaitForSecondsRealtime(gachaAnimDuration);

            if (isDrawing)
            {
                OnGachaAnimationFinished();
            }
        }

        /// <summary>
        /// 애니메이션 클립의 마지막 프레임 Animation Event 또는 연출 완료 시 호출되는 메서드
        /// </summary>
        public void OnGachaAnimationFinished()
        {
            if (!isDrawing) return;

            StopAllCoroutines(); // 진행 중인 안전 타이머 중지

            if (pendingDrawnItems != null && pendingDrawnItems.Count > 0)
            {
                ShowResultPanel(pendingDrawnItems, currentSelectedType);
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
            if (prevGachaButton != null) prevGachaButton.interactable = interactable;
            if (nextGachaButton != null) nextGachaButton.interactable = interactable;
        }

        private void ShowResultPanel(List<ScriptableObject> drawnItems, GachaType type)
        {
            // 결과창이 활성화되는 순간 상자 애니메이터를 idle(닫힌 상태)로 원복
            ResetAnimators();

            if (mainPanel != null) mainPanel.SetActive(false);

            GameObject targetResultPanel = (type == GachaType.Weapon) ? weaponResultPanel : relicResultPanel;
            Transform targetSlotParent = (type == GachaType.Weapon) ? weaponResultSlotParent : relicResultSlotParent;

            if (targetResultPanel != null) targetResultPanel.SetActive(true);

            // 기존 슬롯들을 풀로 반환
            if (targetSlotParent != null && resultSlotPrefab != null)
            {
                foreach (Transform child in targetSlotParent)
                {
                    ObjectPoolManager.Instance.ReturnToPool(resultSlotPrefab.name, child.gameObject);
                }

                foreach (ScriptableObject item in drawnItems)
                {
                    // 오브젝트 풀에서 슬롯 가져오기
                    GameObject slotGO = ObjectPoolManager.Instance.SpawnFromPool(resultSlotPrefab, Vector3.zero, Quaternion.identity);
                    slotGO.transform.SetParent(targetSlotParent, false);

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
            if (weaponResultPanel != null) weaponResultPanel.SetActive(false);
            if (relicResultPanel != null) relicResultPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);

            // 결과창을 닫을 때 열려있던 상자 애니메이터를 닫힌 상태(Idle 0프레임)로 리셋
            ResetAnimators();
        }

        private void ResetAnimators()
        {
            ResetAnimator(weaponAnimator, weaponSubPanel);
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