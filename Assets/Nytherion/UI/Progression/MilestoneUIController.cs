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
    public class MilestoneUIController : MonoBehaviour
    {
        [Header("Data & Prefabs")]
        [Tooltip("복사할 MilestoneSlotUI 프리팹")]
        [SerializeField] private MilestoneSlotUI slotPrefab;
        [SerializeField] private MilestoneData[] milestonesToDisplay;

        [Header("Settings")]
        [SerializeField] private string unifiedTitle = "Milestone";

        private GameObject mainPanel;
        private TMP_Text titleText;
        private Transform slotParent;

        private IObjectResolver container;
        private IProgressionManager progressionManager;
        private bool isPanelActive = false;

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
            if (milestonesToDisplay == null || milestonesToDisplay.Length == 0) Debug.LogWarning(" 표시할 마일스톤 데이터가 없습니다!");

            if (slotPrefab != null && slotParent != null)
            {
                foreach (var milestoneData in milestonesToDisplay)
                {
                    MilestoneSlotUI newSlot = container.Instantiate(slotPrefab, slotParent);
                    newSlot.Initialize(milestoneData);
                    spawnedSlots.Add(newSlot);
                }
            }

            if (mainPanel != null) mainPanel.SetActive(isPanelActive);

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