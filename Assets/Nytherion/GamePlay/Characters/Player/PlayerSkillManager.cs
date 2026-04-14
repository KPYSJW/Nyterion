using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerSkillManager : MonoBehaviour
    {
        public Transform weaponPoint;
        public SkillBase[] equippedSkills = new SkillBase[3];
        public Transform skillHolder;
        void Start()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.onSkillInput += SkillInput;
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.onSkillInput -= SkillInput;
        }

        void SkillInput(int index)
        {
            if (index >= 0 && index < equippedSkills.Length && equippedSkills[index] != null)
            {
                equippedSkills[index].TryUse();
            }
        }
        public void SetEquippedSkills(SkillData[] newSkills)
        {
            for (int i = 0; i < newSkills.Length; i++)
            {
                EquipSkill(newSkills[i], i);
            }
        }

        private void EquipSkill(SkillData newSkillData, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= equippedSkills.Length) return;

            if (equippedSkills[slotIndex] != null)
            {
                Destroy(equippedSkills[slotIndex].gameObject);
                equippedSkills[slotIndex] = null;
            }

            if (newSkillData != null && newSkillData.skillPrefab != null)
            {
                Transform parentTransform = skillHolder != null ? skillHolder : transform;
                GameObject skillInstance = Instantiate(newSkillData.skillPrefab, parentTransform);

                if (skillInstance.TryGetComponent(out SkillBase skillBase))
                {
                    skillBase.skillData = newSkillData;
                    skillBase.caster = transform;
                    skillBase.firePoint = weaponPoint;

                    equippedSkills[slotIndex] = skillBase;
                }
            }
        }
    }
}
