using Nytherion.GamePlay.Combat;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Characters.Items
{
    public class WeaponItem : MonoBehaviour, IUseableItem
    {
        [Header("Weapon Settings")]
        [Tooltip("이 아이템이 나타내는 무기 프리팹")]
        public WeaponBase weapon;

        private PlayerManager _playerManager;

        [Inject]
        public void Construct(PlayerManager playerManager)
        {
            _playerManager = playerManager;
        }

        public void Use()
        {
            if (weapon == null)
            {
                Debug.LogError("Weapon이 할당되지 않았습니다.", this);
                return;
            }

            if (_playerManager == null)
            {
                Debug.LogError("PlayerManager를 찾을 수 없습니다.");
                return;
            }

            if (_playerManager.PlayerCombat == null)
            {
                Debug.LogError("PlayerCombat을 찾을 수 없습니다.");
                return;
            }

            _playerManager.PlayerCombat.EquipWeapon(weapon);
        }
    }
}