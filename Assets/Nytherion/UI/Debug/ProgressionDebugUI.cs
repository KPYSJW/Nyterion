using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Progression;
using System.Collections.Generic;

namespace Nytherion.UI.Test
{
    public class ProgressionDebugUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("테스트할 마일스톤을 선택할 드롭다운")]
        [SerializeField] private TMP_Dropdown milestoneDropdown;

        [Tooltip("진행도를 1씩 올리는 버튼 (옵션)")]
        [SerializeField] private Button addProgressButton;

        [Tooltip("목표값을 한 번에 달성하는(Max) 버튼")]
        [SerializeField] private Button maxProgressButton;

        [Header("Test Data")]
        [Tooltip("디버그 창에서 테스트할 마일스톤 데이터 목록")]
        [SerializeField] private List<MilestoneData> testMilestones = new List<MilestoneData>();

        private IProgressionManager progressionManager;

        [Inject]
        public void Construct(IProgressionManager progressionManager)
        {
            this.progressionManager = progressionManager;
        }

        private void Start()
        {
            if (progressionManager == null)
            {
                Debug.LogError("[ProgressionDebugUI] IProgressionManager가 주입되지 않았습니다!");
                return;
            }

            InitializeDropdown();

            if (addProgressButton != null)
                addProgressButton.onClick.AddListener(OnAddProgressClicked);

            if (maxProgressButton != null)
                maxProgressButton.onClick.AddListener(OnMaxProgressClicked);
        }

        private void InitializeDropdown()
        {
            if (milestoneDropdown == null || testMilestones == null || testMilestones.Count == 0)
            {
                Debug.LogWarning("[ProgressionDebugUI] Dropdown이 없거나 테스트할 Milestone 데이터가 비어있습니다.");
                return;
            }

            milestoneDropdown.ClearOptions();
            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

            foreach (var milestone in testMilestones)
            {
                string optionName = string.IsNullOrEmpty(milestone.title) ? milestone.milestoneID : milestone.title;
                options.Add(new TMP_Dropdown.OptionData(optionName));
            }

            milestoneDropdown.AddOptions(options);
        }

        private void OnAddProgressClicked()
        {
            MilestoneData selectedMilestone = GetSelectedMilestone();
            if (selectedMilestone != null)
            {
                progressionManager.AddProgress(selectedMilestone, 1);
            }
        }

        private void OnMaxProgressClicked()
        {
            MilestoneData selectedMilestone = GetSelectedMilestone();
            if (selectedMilestone != null)
            {
                int currentProgress = progressionManager.GetCurrentProgress(selectedMilestone.milestoneID);

                if (currentProgress == -1 || progressionManager.IsMilestoneCompleted(selectedMilestone.milestoneID))
                {
                    Debug.Log($"[ProgressionDebugUI] '{selectedMilestone.title}' 마일스톤은 이미 달성 완료 상태입니다.");
                    return;
                }

                int remainingAmount = selectedMilestone.targetValue - currentProgress;
                progressionManager.AddProgress(selectedMilestone, remainingAmount);

            }
        }

        private MilestoneData GetSelectedMilestone()
        {
            if (testMilestones == null || testMilestones.Count == 0) return null;
            if (milestoneDropdown == null) return null;

            int selectedIndex = milestoneDropdown.value;
            if (selectedIndex >= 0 && selectedIndex < testMilestones.Count)
            {
                return testMilestones[selectedIndex];
            }
            return null;
        }

        private void OnDestroy()
        {
            if (addProgressButton != null) addProgressButton.onClick.RemoveAllListeners();
            if (maxProgressButton != null) maxProgressButton.onClick.RemoveAllListeners();
        }
    }
}