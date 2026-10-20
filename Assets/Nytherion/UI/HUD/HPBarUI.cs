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
        [SerializeField] private Image shieldFillImage;
        private float displayedHealth;

        private PlayerHealth playerHealth;

        [Inject]
        public void Construct(PlayerHealth playerHealth)
        {
            if (this.playerHealth != null) this.playerHealth.ShieldChanged -= UpdateShield;
            this.playerHealth = playerHealth;
            if (playerHealth != null)
            {
                if (isActiveAndEnabled) playerHealth.ShieldChanged += UpdateShield;
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
                    playerHealth.ShieldChanged += UpdateShield;
                    UpdateHP(playerHealth.CurrentHealth, playerHealth.MaxHealth);
                }
            }
        }
        private void OnEnable()
        {
            PlayerHealth.OnHealthChanged += UpdateHP;
            if (playerHealth != null)
            {
                playerHealth.ShieldChanged -= UpdateShield;
                playerHealth.ShieldChanged += UpdateShield;
                UpdateHP(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }
        }

        private void OnDisable()
        {
            PlayerHealth.OnHealthChanged -= UpdateHP;
            if (playerHealth != null) playerHealth.ShieldChanged -= UpdateShield;
        }

        private void UpdateHP(float current, float max)
        {
            displayedHealth = current;
            if (hpFillImage != null)
            {
                float fillRatio = max > 0f ? current / max : 0f;
                hpFillImage.fillAmount = fillRatio;
            }
            UpdateShield(playerHealth != null ? playerHealth.CurrentShield : 0f,
                playerHealth != null ? playerHealth.MaxShield : 0f);
        }

        private void UpdateShield(float current, float max)
        {
            EnsureShieldOverlay();
            if (shieldFillImage != null)
            {
                if (current > 0f)
                    shieldFillImage.sprite = playerHealth != null && playerHealth.ShieldBarSprite != null
                        ? playerHealth.ShieldBarSprite : hpFillImage != null ? hpFillImage.sprite : null;
                shieldFillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
                shieldFillImage.gameObject.SetActive(current > 0f);
            }
            if (hpText != null)
                hpText.text = current > 0f
                    ? $"{current:0.#}"
                    : $"{displayedHealth}";
        }

        private void EnsureShieldOverlay()
        {
            if (shieldFillImage != null || hpFillImage == null) return;
            // 기존 체력바와 같은 영역에 별도의 실드 이미지를 겹친다.
            GameObject overlay = new GameObject("Shield Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = (RectTransform)overlay.transform;
            RectTransform source = hpFillImage.rectTransform;
            rect.SetParent(source.parent, false);
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition3D = source.anchoredPosition3D;
            rect.localRotation = source.localRotation;
            rect.localScale = source.localScale;
            rect.SetSiblingIndex(source.GetSiblingIndex() + 1);
            shieldFillImage = overlay.GetComponent<Image>();
            shieldFillImage.preserveAspect = false;
            shieldFillImage.type = Image.Type.Filled;
            shieldFillImage.fillMethod = Image.FillMethod.Horizontal;
            shieldFillImage.fillOrigin = 0;
            shieldFillImage.color = Color.white;
            shieldFillImage.raycastTarget = false;
            shieldFillImage.maskable = hpFillImage.maskable;
        }
    }
}
