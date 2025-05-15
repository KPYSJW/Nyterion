using Nytherion.Core;
using Nytherion.Gameplay.Combat;
using Nytherion.Interfaces;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Characters.Player
{
    public class WeaponItem : MonoBehaviour,IUseableItem
    {
        public WeaponBase weapon;
        public void Use()
        {
            Debug.Log("º“»Ø");
           Player.Instance.playerCombat.EquipWeapon(weapon);
        }
    }
}

