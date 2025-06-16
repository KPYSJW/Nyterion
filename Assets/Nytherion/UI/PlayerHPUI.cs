using Nytherion.GamePlay.Characters.Player;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI
{
    public class PlayerHPUI : MonoBehaviour
    {
        public Slider HPSlider;
        public TMP_Text HPText;
        private void Update()
        {
            UpdateHPUI();
        }

        private void UpdateHPUI()
        {
            HPSlider.maxValue = PlayerManager.Instance.playerHealth.MaxHealth;
            HPSlider.value = PlayerManager.Instance.currentHP;
            HPText.text = $"{PlayerManager.Instance.currentHP}";
        }
    }
}

