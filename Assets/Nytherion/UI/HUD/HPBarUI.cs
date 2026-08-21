using UnityEngine;
using UnityEngine.UI;
using Nytherion.GamePlay.Characters.Player;
using TMPro;
using VContainer;

namespace Nytherion.UI.Components
{
    public class HPBarUI : MonoBehaviour
    {
        [SerializeField] private Image hpFillImage;
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
            if (hpFillImage != null)
            {
                float fillRatio = max > 0f ? current / max : 0f;
                hpFillImage.fillAmount = fillRatio;
            }
            hpText.text = $"{current}";
        }
    }
}
