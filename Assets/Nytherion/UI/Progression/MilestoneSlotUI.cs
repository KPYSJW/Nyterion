using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using VContainer; 
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.UI.Components;

namespace Nytherion.UI.Progression
{
    public class MilestoneSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI Components")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Slider progressSlider;

        [Header("Data")]
        [SerializeField] private MilestoneData milestoneData;

        private bool isCompleted = false;
        private IProgressionManager progressionManager;

        [Inject]
        public void Construct(IProgressionManager progressionManager)
        {
            this.progressionManager = progressionManager;
            if (this.progressionManager != null)
            {
                this.progressionManager.OnMilestoneProgressUpdated += HandleProgressUpdated;
                this.progressionManager.OnMilestoneCompleted += HandleMilestoneCompleted;
            }
        }

        private void Start()
        {
            if (milestoneData != null)
            {
                SetupUI();
            }

            if (progressionManager != null)
            {
                UpdateProgressUI(progressionManager);
            }
        }

        private void SetupUI()
        {
            if (milestoneData != null)
            {
                if (titleText != null)
                    titleText.text = milestoneData.title;

                if (iconImage != null && milestoneData.icon != null)
                {
                    iconImage.sprite = milestoneData.icon;
                    iconImage.gameObject.SetActive(true);
                }
            }
            else
            {
                Debug.LogWarning($"[MilestoneSlotUI] '{gameObject.name}' 슬롯에 MilestoneData가 할당되지 않았습니다!");
            }

            RefreshProgress();
        }

        public void Initialize(MilestoneData data)
        {
            milestoneData = data;
            SetupUI();
        }

        private void OnDestroy()
        {
            if (progressionManager != null)
            {
                progressionManager.OnMilestoneProgressUpdated -= HandleProgressUpdated;
                progressionManager.OnMilestoneCompleted -= HandleMilestoneCompleted;
            }
        }

        private void HandleProgressUpdated(string milestoneId, int currentProgress, int targetValue)
        {
            if (milestoneData != null && milestoneData.milestoneID == milestoneId)
            {
                if (progressSlider != null)
                {
                    progressSlider.maxValue = targetValue;
                    progressSlider.value = currentProgress;
                }
            }
        }
        private void HandleMilestoneCompleted(string completedId)
        {
            if (milestoneData != null && milestoneData.milestoneID == completedId)
            {
                this.isCompleted = true;

                if (progressSlider != null)
                {
                    progressSlider.maxValue = milestoneData.targetValue;
                    progressSlider.value = milestoneData.targetValue;
                }
            }
        }

        public void RefreshProgress()
        {
            if (milestoneData == null || progressionManager == null) return;

            isCompleted = progressionManager.IsMilestoneCompleted(milestoneData.milestoneID);
            int currentVal = progressionManager.GetCurrentProgress(milestoneData.milestoneID);
            int targetVal = milestoneData.targetValue;

            if (progressSlider != null)
            {
                if (isCompleted)
                {
                    progressSlider.value = 1f;
                }
                else
                {
                    if (targetVal > 0)
                    {
                        progressSlider.value = (float)currentVal / targetVal;
                    }
                    else
                    {
                        progressSlider.value = 0f;
                        Debug.LogWarning($"[MilestoneSlotUI] '{milestoneData.milestoneID}'의 targetValue가 0이거나 설정되지 않았습니다.");
                    }
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (milestoneData != null && TooltipPanel.Instance != null && progressionManager != null)
            {
                int currentVal = progressionManager.GetCurrentProgress(milestoneData.milestoneID);
                int targetVal = milestoneData.targetValue;
                TooltipPanel.Instance.ShowTooltip(milestoneData, isCompleted, currentVal, targetVal);
            }
            else if (milestoneData == null)
            {
                Debug.LogError($"[MilestoneSlotUI] '{gameObject.name}'에 마우스를 올렸지만, MilestoneData가 할당되지 않아 툴팁을 표시할 수 없습니다! Inspector를 확인해주세요.");
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }
        }
        public void UpdateProgressUI(IProgressionManager manager)
        {
            if (manager == null || milestoneData == null) return;

            int currentProgress = manager.GetCurrentProgress(this.milestoneData.milestoneID);
            this.isCompleted = manager.IsMilestoneCompleted(this.milestoneData.milestoneID);

            if (progressSlider != null)
            {
                progressSlider.maxValue = milestoneData.targetValue;

                if (isCompleted || currentProgress == -1)
                {
                    progressSlider.value = milestoneData.targetValue;
                }
                else
                {
                    progressSlider.value = Mathf.Max(0, currentProgress);
                }
            }
        }
    }
}