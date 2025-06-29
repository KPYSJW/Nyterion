using Nytherion.GamePlay.Combat;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Item
{
    public class WeaponItem : MonoBehaviour, IUseableItem
    {
        [Header("Weapon Settings")]
        [Tooltip("이 아이템이 나타내는 무기 프리팹")]
        public WeaponBase weapon;
        public void Use()
        {
            if (weapon == null)
            {
                Debug.LogError("Weapon이 할당되지 않았습니다.", this);
                return;
            }

            if (PlayerManager.Instance == null)
            {
                Debug.LogError("PlayerManager를 찾을 수 없습니다.");
                return;
            }

            if (PlayerManager.Instance.PlayerCombat == null)
            {
                Debug.LogError("PlayerCombat을 찾을 수 없습니다.");
                return;
            }

            Debug.Log("무기 장착 시도: " + weapon.name);
            PlayerManager.Instance.PlayerCombat.EquipWeapon(weapon);
        }
    }
}