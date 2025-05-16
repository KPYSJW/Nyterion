using Nytherion.Core;
using Nytherion.GamePlay.Combat;
using Nytherion.Interfaces;
using Nytherion.GamePlay.Characters.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Item
{
    public class WeaponItem : MonoBehaviour,IUseableItem
    {
        public WeaponBase weapon;
        public void Use()
        {
            Debug.Log("º“»Ø");

            PlayerManager.Instance.playerCombat.EquipWeapon(weapon);
        
        }
    }
}

