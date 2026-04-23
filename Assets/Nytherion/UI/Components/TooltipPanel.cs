using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.Core.Enums;

namespace Nytherion.UI.Components
{
    public class TooltipPanel : MonoBehaviour
    {
        public static TooltipPanel Instance { get; private set; }

        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image itemImage;

        private void Awake()
        {
            if (Instance == null) 
                Instance = this;
            else 
                Destroy(gameObject);

            canvasGroup.blocksRaycasts = false;
            
            HideTooltip();
        }

        private RectTransform rectTransform;
        private Canvas canvas;
        private Vector2 screenSize;

        private void Start()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            Canvas.willRenderCanvases += OnCanvasRender;
        }

        private void OnDestroy()
        {
            Canvas.willRenderCanvases -= OnCanvasRender;
        }

        private void OnCanvasRender()
        {
            screenSize = canvas.GetComponent<RectTransform>().sizeDelta;
        }

        private void LateUpdate()
        {
            if (!panel.activeSelf) return;

            Vector2 mousePosition = Input.mousePosition;
            
            Vector2 tooltipSize = rectTransform.sizeDelta;
            
            Vector2 pivot = new Vector2(0, 1);
            
            float rightEdge = mousePosition.x + tooltipSize.x * canvas.scaleFactor;
            float bottomEdge = mousePosition.y - tooltipSize.y * canvas.scaleFactor;
            
            if (rightEdge > Screen.width)
            {
                pivot.x = 1;
            }
            
            if (bottomEdge < 0)
            {
                pivot.y = 0;
            }
            
            if (rectTransform.pivot != pivot)
            {
                rectTransform.pivot = pivot;
                rectTransform.position = mousePosition;
            }
            else
            {
                rectTransform.position = mousePosition;
            }
        }

        public void ShowTooltip(ItemData item)
        {
            if (item == null) 
                return;
                
            SetContent(item.itemName, item.description);
            
            if (itemImage != null)
            {
                if (item.icon != null)
                {
                    itemImage.sprite = item.icon;
                    itemImage.preserveAspect = true;
                    itemImage.gameObject.SetActive(true);
                }
                else
                {
                    itemImage.gameObject.SetActive(false);
                }
            }
            
            panel.SetActive(true);
        }
        public void ShowTooltip(SkillData skill, int level = 1, int currentExp = 0, int requiredExp = 1)
        {
            if (skill == null)
                return;

            string skillStats = $"[Lv.{level}] 경험치: {currentExp} / {requiredExp}\n\n" +
                                $"데미지: {skill.damage}\n" +
                                $"쿨타임: {skill.coolDown}초\n" +
                                $"사거리: {skill.range}\n\n" +
                                $"{skill.description}";

            SetContent(skill.skillName, skillStats);

            if (itemImage != null)
            {
                if (skill.icon != null)
                {
                    itemImage.sprite = skill.icon;
                    itemImage.preserveAspect = true;
                    itemImage.gameObject.SetActive(true);
                }
                else
                {
                    itemImage.gameObject.SetActive(false);
                }
            }

            panel.SetActive(true);
        }

        public void ShowTooltip(MilestoneData milestone, bool isCompleted, int currentVal, int targetVal)
        {
            if (milestone == null) return;

            string statusText = isCompleted ? "<color=#00FF00>달성 완료</color>" : $"<color=#FFB400>진행 중 ({currentVal} / {targetVal})</color>";

            string content = $"{milestone.description}\n\n상태: {statusText}";

            if (milestone.rewards != null && milestone.rewards.Count > 0)
            {
                foreach (var reward in milestone.rewards)
                {
                    if (reward.rewardType == RewardType.Skill && reward.skillData != null)
                    {
                        content += $"\n보상: {reward.skillData.skillName} 스킬 획득";
                    }
                    else if (reward.rewardType == RewardType.Gold)
                    {
                        content += $"\n보상: 골드 {reward.amount}";
                    }
                    else if (reward.rewardType == RewardType.Token)
                    {
                        content += $"\n보상: 토큰 {reward.amount}";
                    }
                }
            }

            SetContent(milestone.title, content);

            if (itemImage != null)
            {
                if (milestone.icon != null)
                {
                    itemImage.sprite = milestone.icon;
                    itemImage.preserveAspect = true;
                    itemImage.gameObject.SetActive(true);
                }
                else
                {
                    itemImage.gameObject.SetActive(false);
                }
            }

            panel.SetActive(true);
        }
        public void HideTooltip()
        {
            panel.SetActive(false);
            if (itemImage != null)
            {
                itemImage.gameObject.SetActive(false);
            }
        }

        private void SetContent(string name, string desc)
        {
            if (nameText != null)
                nameText.text = name;
                
            if (descriptionText != null)
                descriptionText.text = desc;
        }
    }
}
