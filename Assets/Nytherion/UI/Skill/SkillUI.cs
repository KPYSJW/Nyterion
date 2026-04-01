using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Skills;
using Nytherion.Data.ScriptableObjects.Skill;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Nytherion.UI.Skill
{
    public class SkillUI : MonoBehaviour
    {
        [System.Serializable]
        public class SkillSlotUI
        {
            public Image icon;
            public Image cooldownOverlay;
            public TMP_Text cooldownText;
            public SkillBase skillBase;
        }

        public SkillSlotUI[] skillSlots= new SkillSlotUI[4];
        
        private PlayerSkillManager playerSkillManager;
        
        [Inject]
        public void Construct(PlayerSkillManager playerSkillManager)
        {
            this.playerSkillManager = playerSkillManager;
        }

        private void Update()
        {
            UpdateSkill();
            CooltimeCheck();
        }
        void UpdateSkill()
        {
            if (playerSkillManager == null) return;

            for (int i = 0; i < skillSlots.Length; i++)
            {
                SkillBase equippedSkill = playerSkillManager.equippedSkills[i];

                if (equippedSkill == null || equippedSkill.skillData == null)
                {
                    skillSlots[i].icon.gameObject.SetActive(false);
                    skillSlots[i].skillBase = null;
                }
                else
                {
                    skillSlots[i].icon.gameObject.SetActive(true);
                    skillSlots[i].icon.sprite = equippedSkill.skillData.icon; 
                    skillSlots[i].skillBase = equippedSkill;
                }
            }
        }

        void CooltimeCheck()
        {
            if (playerSkillManager == null) return;

            for (int i = 0; i < skillSlots.Length; i++)
            {
                SkillBase skill = skillSlots[i].skillBase;

                if (skill == null || skill.skillData == null)
                {
                    skillSlots[i].cooldownOverlay.fillAmount = 0f;
                    skillSlots[i].cooldownText.text = "";
                    continue;
                }

                float total = skill.GetCooldownTime();
                float remain = skill.GetRemainingCooldown();

                if (remain > 0f)
                {
                    skillSlots[i].cooldownOverlay.fillAmount = remain / total;
                    skillSlots[i].cooldownText.text = Mathf.CeilToInt(remain).ToString();
                }
                else
                {
                    skillSlots[i].cooldownOverlay.fillAmount = 0f;
                    skillSlots[i].cooldownText.text = "";
                }
            }
        }
    }
}

