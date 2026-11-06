using UnityEngine;
using UnityEngine.UI;
using Nytherion.GamePlay.Combat.Weapons;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerChargeBarUI : MonoBehaviour
    {
        private PlayerCombat playerCombat;

        [Header("UI Slider Reference")]
        [Tooltip("차징 진행도를 표시할 UI Slider 컴포넌트")]
        [SerializeField] private Slider chargeSlider;

        [Tooltip("풀차징 상태에서 표시할 Fill 스프라이트")]
        [SerializeField] private Sprite fullChargeFillSprite;

        private Image chargeFillImage;
        private Sprite defaultChargeFillSprite;

        private void Start()
        {
            playerCombat = GetComponent<PlayerCombat>();
            
            if (chargeSlider != null)
            {
                if (chargeSlider.fillRect != null)
                {
                    chargeFillImage = chargeSlider.fillRect.GetComponent<Image>();
                    if (chargeFillImage != null)
                    {
                        defaultChargeFillSprite = chargeFillImage.sprite;
                    }
                }

                chargeSlider.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (playerCombat == null || chargeSlider == null) return;

            if (playerCombat.IsGenericCharging)
            {
                if (!chargeSlider.gameObject.activeSelf)
                {
                    chargeSlider.gameObject.SetActive(true);
                }
                UpdateChargeProgress(playerCombat.GenericChargePercent);
                return;
            }

            IChargeableWeapon chargeWeapon = playerCombat.currentWeapon as IChargeableWeapon;

            if (chargeWeapon != null && chargeWeapon.IsCharging)
            {
                if (!chargeSlider.gameObject.activeSelf)
                {
                    chargeSlider.gameObject.SetActive(true);
                }

                // 차징 퍼센트(0f ~ 1f)를 슬라이더 값으로 적용
                UpdateChargeProgress(chargeWeapon.ChargePercent);
            }
            else
            {
                UpdateChargeProgress(0f);
                if (chargeSlider.gameObject.activeSelf)
                {
                    chargeSlider.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateChargeProgress(float chargePercent)
        {
            chargeSlider.value = chargePercent;
            if (chargeFillImage == null) return;

            Sprite fillSprite = chargePercent >= 1f && fullChargeFillSprite != null
                ? fullChargeFillSprite
                : defaultChargeFillSprite;
            if (chargeFillImage.sprite != fillSprite)
            {
                chargeFillImage.sprite = fillSprite;
            }
        }
    }
}
