using Nytherion.GamePlay.Combat;
using Nytherion.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Nytherion.Data.ScriptableObjects.Synergy;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 플레이어의 전투 관련 기능을 관리하는 클래스입니다.
    /// 무기 장착, 공격 입력 처리, 전투 상태 관리 등을 담당합니다.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private Transform weaponPoint;
        public WeaponBase currentWeapon;
       
        /// <summary>
        /// 컴포넌트가 활성화될 때 호출됩니다.
        /// 필요한 이벤트들을 구독합니다.
        /// </summary>
        private void Start()
        {
            
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onAttackDown += Attack;
                InputManager.Instance.onAttackUp += AttackEnd;
            }
        }

    

        /// <summary>
        /// 새로운 무기를 장착합니다.
        /// 기존 무기가 있는 경우 제거한 후 새 무기를 생성합니다.
        /// </summary>
        /// <param name="weapon">장착할 무기 프리팹</param>
        public void EquipWeapon(WeaponBase weapon)
        {
            if (currentWeapon!=null)
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

        /// <summary>
        /// 지정된 방향으로 공격을 시도합니다.
        /// </summary>
        /// <param name="direction">공격 방향 (정규화된 벡터)</param>
        public void Attack()
        {
            if (currentWeapon != null)
            {
                currentWeapon.Attack(Vector2.right);
            }
        }

        /// <summary>
        /// 공격 종료를 처리합니다.
        /// 현재 장착된 무기의 AttackEnd 메서드를 호출합니다.
        /// </summary>
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
                InputManager.Instance.onAttackUp -= Attack;
                InputManager.Instance.onAttackUp -= AttackEnd;
            }
        }

    }
}

