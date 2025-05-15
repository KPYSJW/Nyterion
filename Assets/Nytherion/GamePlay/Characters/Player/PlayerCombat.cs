using Nytherion.Combat;
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
        private WeaponBase currentWeapon;

        private void Start()
        {
            EquipWeapon(weaponPrefab);
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
            currentWeapon.Attack(Vector2.up);
        }
        void AttackEnd()
        {
            currentWeapon.AttackEnd();
        }
    }
}

