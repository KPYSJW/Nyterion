using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Characters.Skill;
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
            public SkillBase SkillBase;
        }

        public SkillSlotUI[] skillSlots= new SkillSlotUI[4];
        
        private PlayerSkillManager _playerSkillManager;
        
        [Inject]
        public void Construct(PlayerSkillManager playerSkillManager)
        {
            _playerSkillManager = playerSkillManager;
        }

        private void Update()
        {
            UpdateSkill();
            CooltimeCheck();
        }
        void UpdateSkill()
        {
            if (_playerSkillManager == null) return;
            
            for(int i=0;i<skillSlots.Length; i++)
            {
                if(_playerSkillManager.equippedSkills[i]==null) skillSlots[i].icon.gameObject.SetActive(false);
                else
                {
                    skillSlots[i].icon.sprite = _playerSkillManager.equippedSkills[i].skillData.icon;
                    skillSlots[i].SkillBase = _playerSkillManager.equippedSkills[i];
                }
               
            }
        }

       void CooltimeCheck()
        {
            for (int i = 0; i < skillSlots.Length; i++)
            {
                if (skillSlots[i].SkillBase == null) continue;
                float total = skillSlots[i].SkillBase.GetCooldownTime();
                float remain = skillSlots[i].SkillBase.GetRemainingCooldown();

                if(remain>0f)
                {
                    skillSlots[i].cooldownOverlay.fillAmount= remain/total;
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

