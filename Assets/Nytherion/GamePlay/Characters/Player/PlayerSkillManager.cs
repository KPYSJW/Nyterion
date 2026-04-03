using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using System;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerSkillManager : MonoBehaviour
    {
        public Transform weaponPoint;
        public SkillBase[] equippedSkills = new SkillBase[4];
        public Transform skillHolder;
        public SkillData[] startingSkills = new SkillData[4];
        private InputManager inputManager;


        void Start()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onSkillInput += SkillInput;
            }
            else
            {
                Debug.LogError("[PlayerSkillManager] InputManager.Instance가 없습니다! (InputManager가 씬에 있는지 확인하세요)");
            }

            for (int i = 0; i < startingSkills.Length; i++)
            {
                if (startingSkills[i] != null)
                {
                    EquipSkill(startingSkills[i], i);
                }
            }
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onSkillInput -= SkillInput;
            }
        }

        void SkillInput(int index)
        {
            Debug.Log($"[PlayerSkillManager] {index}번 스킬 키 입력 감지됨!");

            if (index >= 0 && index < equippedSkills.Length && equippedSkills[index] != null)
            {
                equippedSkills[index].TryUse();
                Debug.Log($"[PlayerSkillManager] {index}번 슬롯의 스킬 TryUse() 호출됨!");
            }
            else
            {
                Debug.LogWarning($"[PlayerSkillManager] {index}번 슬롯이 비어있어서 발동 불가!");
            }
        }
        public void EquipSkill(SkillData newSkillData, int slotIndex)
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
                else
                {
                    Debug.LogError("스킬 프리팹에 SkillBase 컴포넌트가 없습니다!");
                }
            }
        }
    }
}

