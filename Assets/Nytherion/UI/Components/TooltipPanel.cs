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
            if (!panel.activeSelf || canvas == null) return;

            Vector2 mousePosition = Input.mousePosition;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            
            // Layout이 적용된 후의 실제 픽셀 크기를 가져옴
            float width = panelRect.rect.width * canvas.scaleFactor;
            float height = panelRect.rect.height * canvas.scaleFactor;
            
            Vector2 pivot = new Vector2(0, 1);
            Vector2 offset = new Vector2(15, -15); // 커서와 겹치지 않게 여유 공간
            
            // 1. 가로 위치 결정 (오른쪽 넘침 시 왼쪽으로)
            if (mousePosition.x + width + offset.x > Screen.width)
            {
                pivot.x = 1;
                offset.x = -15;
            }
            
            // 2. 세로 위치 결정 (아래쪽 넘침 시 위쪽으로)
            if (mousePosition.y - height + offset.y < 0)
            {
                pivot.y = 0;
                offset.y = 15;
            }
            
            // 3. 최종 위치 계산 및 화면 안으로 강제 제한 (Clamping)
            // 피벗이 적용된 상태에서의 최종 월드 좌표를 계산
            if (panelRect.pivot != pivot) panelRect.pivot = pivot;
            
            Vector2 finalPos = mousePosition + (offset * canvas.scaleFactor);
            
            // 화면 밖으로 나가지 않도록 최종 보정
            // x축 제한
            float minX = pivot.x * width;
            float maxX = Screen.width - (1 - pivot.x) * width;
            finalPos.x = Mathf.Clamp(finalPos.x, minX, maxX);
            
            // y축 제한
            float minY = pivot.y * height;
            float maxY = Screen.height - (1 - pivot.y) * height;
            finalPos.y = Mathf.Clamp(finalPos.y, minY, maxY);
            
            panelRect.position = finalPos;
        }

        public void ShowTooltip(ItemData item)
        {
            if (item == null) 
                return;
                
            string finalDesc = item.description;

            if (item is Nytherion.Data.ScriptableObjects.Items.EquipmentData equipData)
            {
                var playerManager = FindObjectOfType<Nytherion.Core.Managers.PlayerManager>();
                bool shouldInvert = false;
                
                if (playerManager != null && equipData.traits != null)
                {
                    foreach (var trait in equipData.traits)
                    {
                        if (playerManager.IsTraitInverted(trait))
                        {
                            shouldInvert = true;
                            break;
                        }
                    }
                }

                if (equipData is Nytherion.Data.ScriptableObjects.Weapons.WeaponData weaponData)
                {
                    float finalDamage = weaponData.damage;
                    float finalCooldown = weaponData.cooldown;

                    if (equipData.statModifiers != null)
                    {
                        foreach (var mod in equipData.statModifiers)
                        {
                            float modValue = mod.value;
                            if (shouldInvert && modValue < 0)
                            {
                                modValue = Mathf.Abs(modValue);
                            }

                            if (mod.stat == StatType.MeleeDamage || mod.stat == StatType.RangedDamage)
                            {
                                if (!mod.isPercentage) finalDamage += modValue;
                                else finalDamage *= (1 + modValue);
                            }
                            else if (mod.stat == StatType.MeleeSpeed || mod.stat == StatType.RangedSpeed)
                            {
                                // 쿨타임은 스피드가 오를수록 줄어드는 방식이거나 직접 합산일 수 있으므로 플랫 수치로 계산
                                if (!mod.isPercentage) finalCooldown -= modValue; // 속도가 플러스면 쿨타임 감소
                                else finalCooldown *= (1 - modValue);
                            }
                        }
                    }

                    // 0 이하로 떨어지지 않게 보정
                    finalDamage = Mathf.Max(0, finalDamage);
                    finalCooldown = Mathf.Max(0.1f, finalCooldown);

                    string weaponStats = $"<color=#FFD700>[공격력: {finalDamage:F1}]</color>\n";
                    weaponStats += $"<color=#00FFFF>[공격 속도: {finalCooldown:F1}초]</color>\n\n";

                    finalDesc = weaponStats + finalDesc;
                }

                // 모디파이어 텍스트 생성
                if (equipData.statModifiers != null && equipData.statModifiers.Count > 0)
                {
                    finalDesc += "\n\n<color=#AAAAAA><추가 능력치></color>";
                    foreach (var mod in equipData.statModifiers)
                    {
                        float displayValue = mod.value;
                        bool inverted = false;

                        if (shouldInvert && displayValue < 0)
                        {
                            displayValue = Mathf.Abs(displayValue);
                            inverted = true;
                        }

                        string sign = displayValue > 0 ? "+" : "";
                        string percent = mod.isPercentage ? "%" : "";
                        string color = displayValue > 0 ? "#00FF00" : "#FF0000";
                        if (inverted) color = "#FF00FF"; // 반전 시 특별한 색상

                        finalDesc += $"\n<color={color}>{mod.stat} {sign}{displayValue}{percent}</color>";
                        if (inverted) finalDesc += " <color=#FF00FF>(반전됨!)</color>";
                    }
                }
            }
            
            SetContent(item.itemName, finalDesc);
            
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
