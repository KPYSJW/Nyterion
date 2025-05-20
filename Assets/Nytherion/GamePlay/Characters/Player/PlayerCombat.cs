using Nytherion.GamePlay.Combat;
using Nytherion.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 플레이어의 전투 관련 기능을 관리하는 클래스입니다.
    /// 무기 장착, 공격 입력 처리, 전투 상태 관리 등을 담당합니다.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        /// <summary>무기가 생성될 위치를 지정하는 트랜스폼</summary>
        [Tooltip("무기가 생성될 위치를 지정하는 트랜스폼")]
        [SerializeField] private Transform weaponPoint;
        
        /// <summary>현재 장착된 무기 인스턴스</summary>
        [Tooltip("현재 플레이어가 장착한 무기")]
        public WeaponBase currentWeapon;

        /// <summary>
        /// 컴포넌트가 활성화될 때 호출됩니다.
        /// 필요한 이벤트들을 구독합니다.
        /// </summary>
        /// <summary>
        /// 컴포넌트가 활성화될 때 호출됩니다.
        /// 입력 매니저의 이벤트에 메서드를 연결합니다.
        /// </summary>
        private void Start()
        {
            // 입력 매니저가 존재하는 경우에만 이벤트 구독
            if (InputManager.Instance != null)
            {
                // 공격 시작 이벤트에 Attack 메서드 연결
                InputManager.Instance.onAttackDown += Attack;
                // 공격 종료 이벤트에 AttackEnd 메서드 연결
                InputManager.Instance.onAttackUp += AttackEnd;
            }
        }

        /// <summary>
        /// 새로운 무기를 장착합니다.
        /// 기존 무기가 있는 경우 제거한 후 새 무기를 생성합니다.
        /// </summary>
        /// <param name="weapon">장착할 무기 프리팹</param>
        /// <summary>
        /// 새로운 무기를 장착합니다.
        /// 기존에 장착된 무기가 있는 경우 제거한 후 새 무기를 생성합니다.
        /// </summary>
        /// <param name="weapon">장착할 무기 프리팹</param>
        public void EquipWeapon(WeaponBase weapon)
        {
            // 이미 무기가 장착되어 있는 경우 기존 무기 제거
            if (currentWeapon != null)
            {
                Destroy(currentWeapon.gameObject);
            }
            
            // 새 무기를 weaponPoint 위치에 생성하고 자식으로 설정
            currentWeapon = Instantiate(weapon, weaponPoint.position, Quaternion.identity, weaponPoint);
        }

        /// <summary>
        /// 지정된 방향으로 공격을 시도합니다.
        /// </summary>
        /// <param name="direction">공격 방향 (정규화된 벡터)</param>
        /// <summary>
        /// 공격을 시작합니다.
        /// 현재 장착된 무기가 있는 경우, 위쪽 방향으로 공격 명령을 전달합니다.
        /// </summary>
        public void Attack()
        {
            // 무기가 장착되어 있는 경우에만 공격 실행
            if (currentWeapon != null)
            {
                // 기본적으로 위쪽 방향으로 공격 (실제 게임에서는 마우스 방향이나 조이스틱 입력에 따라 달라질 수 있음)
                currentWeapon.Attack(Vector2.up);
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
                currentWeapon.AttackEnd();
            }
        }

        /// <summary>
        /// 컴포넌트가 비활성화될 때 호출됩니다.
        /// 등록된 이벤트를 해제하여 메모리 누수를 방지합니다.
        /// </summary>
        private void OnDisable()
        {
            // 입력 매니저가 존재하는 경우에만 이벤트 구독 해제
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onAttackDown -= Attack;  // 공격 시작 이벤트 해제
                InputManager.Instance.onAttackUp -= AttackEnd;  // 공격 종료 이벤트 해제
            }
        }

    }
}

