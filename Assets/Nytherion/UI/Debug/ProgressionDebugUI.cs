using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Progression;
using System.Collections.Generic;

namespace Nytherion.UI.Test
{
    public class ProgressionDebugUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("등록된 모든 테스트 마일스톤의 진행도를 1씩 올리는 버튼 (옵션)")]
        [SerializeField] private Button addProgressButton;

        [Tooltip("등록된 모든 테스트 마일스톤을 목표값까지 달성하는(Max) 버튼")]
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

            if (addProgressButton != null)
                addProgressButton.onClick.AddListener(OnAddProgressClicked);

            if (maxProgressButton != null)
                maxProgressButton.onClick.AddListener(OnMaxProgressClicked);
        }

        private void OnAddProgressClicked()
        {
            if (progressionManager == null || testMilestones == null) return;

            foreach (MilestoneData milestone in testMilestones)
            {
                if (milestone == null) continue;

                progressionManager.AddProgress(milestone, 1);
            }
        }

        private void OnMaxProgressClicked()
        {
            if (progressionManager == null || testMilestones == null) return;

            foreach (MilestoneData milestone in testMilestones)
            {
                if (milestone == null) continue;

                int currentProgress = progressionManager.GetCurrentProgress(milestone.milestoneID);

                if (currentProgress == -1 || progressionManager.IsMilestoneCompleted(milestone.milestoneID))
                {
                    Debug.Log($"[ProgressionDebugUI] '{milestone.title}' 마일스톤은 이미 달성 완료 상태입니다.");
                    continue;
                }

                int remainingAmount = milestone.targetValue - currentProgress;
                progressionManager.AddProgress(milestone, remainingAmount);
            }
        }

        private void OnDestroy()
        {
            if (addProgressButton != null) addProgressButton.onClick.RemoveAllListeners();
            if (maxProgressButton != null) maxProgressButton.onClick.RemoveAllListeners();
        }
    }
}
