using Nytherion.Core;
using Nytherion.GamePlay.Combat;
using Nytherion.Interfaces;
using Nytherion.GamePlay.Characters.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Item
{
    /// <summary>
    /// 플레이어가 획득하여 사용할 수 있는 무기 아이템 클래스입니다.
    /// IUseableItem 인터페이스를 구현하여 인벤토리 시스템과 연동됩니다.
    /// </summary>
    public class WeaponItem : MonoBehaviour, IUseableItem
    {
        [Header("Weapon Settings")]
        [Tooltip("이 아이템이 나타내는 무기 프리팹")]
        public WeaponBase weapon;
        /// <summary>
        /// 아이템을 사용(장착)합니다.
        /// PlayerManager를 통해 현재 플레이어에게 무기를 장착시킵니다.
        /// </summary>
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