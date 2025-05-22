using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Characters.Skill;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI
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

        private void Update()
        {
            UpdateSkill();
            CooltimeCheck();
        }
        void UpdateSkill()//������ ������Ʈ�����ְ� ���߿��� ��ų�� ���Ҷ��� �ҷ��� ����
        {
            for(int i=0;i<skillSlots.Length; i++)
            {
                if(PlayerSkillManager.Instance.equippedSkills[i]==null) skillSlots[i].icon.gameObject.SetActive(false);
                else
                {
                    skillSlots[i].icon.sprite = PlayerSkillManager.Instance.equippedSkills[i].skillData.icon;
                    skillSlots[i].SkillBase = PlayerSkillManager.Instance.equippedSkills[i];
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

