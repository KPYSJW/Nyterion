using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Skill;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerSkillManager : MonoBehaviour
    {
        public SkillBase[] equippedSkills = new SkillBase[4];

        private InputManager _inputManager;

        [Inject]
        public void Construct(InputManager inputManager)
        {
            _inputManager = inputManager;
        }


        void Start()
        {
            if (_inputManager != null)
            {
                _inputManager.onSkillInput += SkillInput;
            }
        }

        private void OnDestroy()
        {
            if (_inputManager != null)
            {
                _inputManager.onSkillInput -= SkillInput;
            }
        }

        void SkillInput(int i)
        {
            if (equippedSkills[i] != null)
            {
                equippedSkills[i].TryUse();
            }
            else
            {
                Debug.Log($"Equipped skill is null at index {i}");
            }
        }
        
        void UseSkill(int i)
        {
            
        }
    }
}

