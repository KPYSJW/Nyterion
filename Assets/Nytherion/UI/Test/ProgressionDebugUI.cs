using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;

namespace Nytherion.UI.Test
{
    public class ProgressionDebugUI : MonoBehaviour
    {
        [Header("Test Buttons")]
        public Button unlockFireballButton;
        public Button completeMilestoneButton;

        private IProgressionManager progressionManager;

        [Inject]
        public void Construct(IProgressionManager progressionManager)
        {
            this.progressionManager = progressionManager;
        }

        private void Start()
        {
            if (unlockFireballButton != null)
            {
                unlockFireballButton.onClick.AddListener(() =>
                {
                    if (progressionManager != null)
                    {
                        progressionManager.UnlockSkill(SkillType.FireBall);
                    }
                    else
                    {
                        Debug.LogError("[DebugUI] ProgressionManager가 주입되지 않았습니다!");
                    }
                });
            }

            if (completeMilestoneButton != null)
            {
                completeMilestoneButton.onClick.AddListener(() =>
                {
                    if (progressionManager != null)
                    {
                        progressionManager.CompleteMilestone("Test_Boss_Defeated");
                    }
                });
            }
        }
    }
}