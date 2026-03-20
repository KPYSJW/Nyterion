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

        private PlayerHealth playerHealth;

        [Inject]
        public void Construct(PlayerHealth playerHealth)
        {
            this.playerHealth = playerHealth;
            if (playerHealth != null)
            {
                UpdateHP(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }
        }
        private void Start()
        {
            if (playerHealth == null)
            {
                playerHealth = FindObjectOfType<PlayerHealth>();
                if (playerHealth != null)
                {
                    UpdateHP(playerHealth.CurrentHealth, playerHealth.MaxHealth);
                }
            }
        }
        private void OnEnable()
        {
            PlayerHealth.OnHealthChanged += UpdateHP;
            if (playerHealth != null)
            {
                UpdateHP(playerHealth.CurrentHealth, playerHealth.MaxHealth);
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
