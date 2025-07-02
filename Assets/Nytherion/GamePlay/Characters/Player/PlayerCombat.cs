using Nytherion.GamePlay.Combat;
using Nytherion.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Synergy;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [Tooltip("무기가 생성될 위치를 지정하는 트랜스폼")]
        [SerializeField] private Transform weaponPoint;
        
        [Tooltip("현재 플레이어가 장착한 무기")]
        public WeaponBase currentWeapon;
       
        private void Start()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onAttackDown += Attack;
                InputManager.Instance.onAttackUp += AttackEnd;
                InputManager.Instance.onAttackUp += AttackEnd;
            }
        }
        public void EquipWeapon(WeaponBase weapon)
        {
            if (currentWeapon != null)
            {
                Destroy(currentWeapon.gameObject);
            }
            WeaponEngravingSynergyData synergy = PlayerManager.Instance.playerEngravingManager.synergyEvaluator.EvaluateSynergy(weapon.weaponData, PlayerManager.Instance.playerEngravingManager.GetCurrentEngravings());
            if (synergy != null)
            {
                Debug.Log($"✅ 시너지 발동: {synergy.weaponName} + {synergy.engravingName}");
            }
            else
            {
                Debug.Log("❌ 시너지 없음.");
            }
            currentWeapon = Instantiate(weapon, weaponPoint.position, Quaternion.identity, weaponPoint);
        }

        public void Attack()
        {
            if (currentWeapon != null)
            {
                currentWeapon.Attack(Vector2.right);

            }
        }

        public void AttackEnd()
        {
            if (currentWeapon != null)
            {
                //currentWeapon.AttackEnd();
            }
        }

        private void OnDisable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onAttackDown -= Attack;  
                InputManager.Instance.onAttackUp -= AttackEnd;  
            }
        }

    }
}

