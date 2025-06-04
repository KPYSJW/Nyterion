using Nytherion.Core;
using Nytherion.GamePlay.Characters.Skill;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerSkillManager : MonoBehaviour
    {
        public SkillBase[] equippedSkills = new SkillBase[4];
        public static PlayerSkillManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void Start()
        {
            InputManager.Instance.onSkillInput += SkillInput;
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
    }
}

