using Nytherion.Gameplay.Combat;
using Nytherion.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Characters.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private Transform weaponPoint;
        [SerializeField] private WeaponBase weaponPrefab;
        public WeaponBase currentWeapon;

        private void Start()
        {
            
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onAttackDown += Attack;
                InputManager.Instance.onAttackUp += AttackEnd;
            }
        }
        public void EquipWeapon(WeaponBase weapon)
        {
            if (currentWeapon!=null)
            {
                Destroy(currentWeapon.gameObject);
            }
            currentWeapon = Instantiate(weapon, weaponPoint.position, Quaternion.identity, weaponPoint);
        }

    

        private void OnDisable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onAttackUp -= Attack;
                InputManager.Instance.onAttackUp -= AttackEnd;
            }
        }

        void Attack()
        {
            if (currentWeapon != null)
            {
                currentWeapon.Attack(Vector2.up);
            }
        }
        void AttackEnd()
        {
            if (currentWeapon != null)
            {
                currentWeapon.AttackEnd();
            }
        }

    }
}

