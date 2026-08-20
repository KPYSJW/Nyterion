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

        private void Start()
        {
            playerCombat = GetComponent<PlayerCombat>();
            
            if (chargeSlider != null)
            {
                chargeSlider.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (playerCombat == null || chargeSlider == null) return;

            if (playerCombat.currentWeapon is FrenzyWeapon)
            {
                if (chargeSlider.gameObject.activeSelf)
                {
                    chargeSlider.gameObject.SetActive(false);
                }
                return;
            }

            if (playerCombat.IsGenericCharging)
            {
                if (!chargeSlider.gameObject.activeSelf)
                {
                    chargeSlider.gameObject.SetActive(true);
                }
                chargeSlider.value = playerCombat.GenericChargePercent;
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
                chargeSlider.value = chargeWeapon.ChargePercent;
            }
            else
            {
                if (chargeSlider.gameObject.activeSelf)
                {
                    chargeSlider.gameObject.SetActive(false);
                }
            }
        }
    }
}
