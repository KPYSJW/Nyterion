using System.Collections.Generic;
using UnityEngine;
using TMPro;
using VContainer;
using VContainer.Unity;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.UI.Components;

namespace Nytherion.UI.Progression
{
    public class MilestoneUIController : UIPanelBase
    {
        [Header("Data & Prefabs")]
        [Tooltip("복사할 MilestoneSlotUI 프리팹")]
        [SerializeField] private MilestoneSlotUI slotPrefab;
        [SerializeField] private MilestoneData[] milestonesToDisplay;
        [SerializeField] private MilestoneDatabaseSO milestoneDatabase;

        [Header("Settings")]
        [SerializeField] private string unifiedTitle = "Milestone";

        private GameObject mainPanel;
        private TMP_Text titleText;
        private Transform slotParent;

        private IObjectResolver container;
        private IProgressionManager progressionManager;
        // private bool isPanelActive = false; // IsOpen으로 대체

        private List<MilestoneSlotUI> spawnedSlots = new List<MilestoneSlotUI>();
        [Inject]
        public void Construct(
            IObjectResolver container,
            GameSceneUIRefs uiRefs,
            IProgressionManager progressionManager
            ) 
        {
            this.container = container;
            this.progressionManager = progressionManager; 

            this.mainPanel = uiRefs.ProgressionMainPanel;
            this.titleText = uiRefs.ProgressionTitleText;
            this.slotParent = uiRefs.ProgressionSlotParent;

            // --- UIPanelBase 연동을 위한 설정 추가 ---
            if (mainPanel != null)
            {
                // 자신 또는 부모에게서 CanvasGroup을 찾습니다.
                this.controlledCanvasGroup = mainPanel.GetComponent<CanvasGroup>();
                if (this.controlledCanvasGroup == null)
                {
                    this.controlledCanvasGroup = mainPanel.GetComponentInParent<CanvasGroup>();
                }
            }
            // --------------------------------------
        }

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            if (container == null)
            {
                Debug.LogError($"[{gameObject.name}] IObjectResolver(container)가 주입되지 않았습니다!");
                return;
            }

            if (titleText != null) titleText.text = unifiedTitle;

            if (slotPrefab == null) Debug.LogError(" slotPrefab이 할당되지 않았습니다! (MilestoneUIController 인스펙터 확인)");
            if (slotParent == null) Debug.LogError(" slotParent가 null입니다! (GameSceneUIRefs의 ProgressionSlotParent 할당 확인)");

            // 표시할 마일스톤 목록 결정 (직접 지정된 목록 우선, 없으면 데이터베이스 전체)
            System.Collections.Generic.List<MilestoneData> finalMilestones = new System.Collections.Generic.List<MilestoneData>();
            if (milestonesToDisplay != null && milestonesToDisplay.Length > 0)
            {
                finalMilestones.AddRange(milestonesToDisplay);
            }
            else if (progressionManager != null)
            {
                finalMilestones.AddRange(progressionManager.GetAllMilestones());
            }

            if (finalMilestones.Count == 0) Debug.LogWarning(" 표시할 마일스톤 데이터가 없습니다!");

            if (slotPrefab != null && slotParent != null)
            {
                foreach (MilestoneData milestoneData in finalMilestones)
                {
                    if (milestoneData == null) continue;
                    MilestoneSlotUI newSlot = container.Instantiate(slotPrefab, slotParent);
                    newSlot.Initialize(milestoneData);
                    spawnedSlots.Add(newSlot);
                }
            }

            // 시작 시 UI 패널 숨김 처리 (에디터에서 꺼져있을 수 있으므로 강제 동기화)
            if (mainPanel != null) mainPanel.SetActive(false);

            if (InputManager.Instance != null) InputManager.Instance.onToggleProgressionUI += TogglePanel;

            if (progressionManager is ProgressionManager pManager)
                pManager.OnProgressionDataLoaded += RefreshUI;

            RefreshUI();
        }
        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onToggleProgressionUI -= TogglePanel;
            }
            if (progressionManager is ProgressionManager pManager)
            {
                pManager.OnProgressionDataLoaded -= RefreshUI;
            }
        }

        private void TogglePanel()
        {
            // --- 런타임 null 체크 보강 ---
            if (controlledCanvasGroup == null && mainPanel != null)
            {
                this.controlledCanvasGroup = mainPanel.GetComponent<CanvasGroup>();
                if (this.controlledCanvasGroup == null)
                    this.controlledCanvasGroup = mainPanel.GetComponentInParent<CanvasGroup>();
            }
            // ----------------------------

            /* 기존 방식 주석 처리
            if (mainPanel == null) return;

            isPanelActive = !isPanelActive;
            mainPanel.SetActive(isPanelActive);

            if (isPanelActive)
            {
                RefreshUI();
            }
            else
            {
                if (TooltipPanel.Instance != null)
                {
                    TooltipPanel.Instance.HideTooltip();
                }
            }
            */

            // UIPanelBase 시스템 사용
            Toggle();
        }

        public override void Open(bool closeOthers = true)
        {
            base.Open(closeOthers);
            RefreshUI();
        }

        public override void Close()
        {
            base.Close();
            if (TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }
        }

        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (mainPanel != null)
            {
                mainPanel.SetActive(isOpen);
            }
        }

        private void RefreshUI()
        {
            if (progressionManager == null) return;

            foreach (var slot in spawnedSlots)
            {
                slot.UpdateProgressUI(progressionManager);
            }
        }
    }
}
