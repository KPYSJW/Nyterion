using UnityEngine;
using UnityEngine.UI;
using Nytherion.GamePlay.Characters.Player;
using TMPro;

namespace Nytherion.UI
{
    public class HPBarUI : MonoBehaviour
    {
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI hpText;
        private void OnEnable()
        {
            PlayerHealth.OnHealthChanged += UpdateHP;
        }

        private void OnDisable()
        {
            PlayerHealth.OnHealthChanged -= UpdateHP;
        }

        private void UpdateHP(float current, float max)
        {
            hpSlider.maxValue = max;
            hpSlider.value = current;
            hpText.text = $"{current}";
        }
    }
}
