using UnityEngine;
using UnityEngine.UI;
using Nytherion.GamePlay.Characters.Player;
using TMPro;
using VContainer;

namespace Nytherion.UI.Components
{
    public class HPBarUI : MonoBehaviour
    {
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI hpText;

        private PlayerHealth _playerHealth;

        [Inject]
        public void Construct(PlayerHealth playerHealth)
        {
            _playerHealth = playerHealth;
        }

        private void OnEnable()
        {
            if (_playerHealth != null)
            {
                PlayerHealth.OnHealthChanged += UpdateHP;
                UpdateHP(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
            }
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
