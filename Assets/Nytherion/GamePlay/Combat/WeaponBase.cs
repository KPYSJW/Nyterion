using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Weapons;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 모든 무기 클래스의 기본이 되는 추상 클래스입니다.
    /// 이 클래스를 상속받아 다양한 유형의 무기(근접, 원거리 등)를 구현할 수 있습니다.
    /// 무기의 공통적인 기능과 인터페이스를 정의합니다.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        /// <summary>
        /// 무기 데이터입니다. 이 데이터는 무기의 속성과 행동을 정의합니다.
        /// ScriptableObject를 사용하여 다양한 무기 유형을 쉽게 생성하고 관리할 수 있습니다.
        /// </summary>
        [Tooltip("무기의 속성과 행동을 정의하는 데이터")]
        [SerializeField] protected WeaponData weaponData;

        /// <summary>
        /// 마지막 공격 시간입니다.
        /// Time.time을 기준으로 저장되며, 공격 쿨다운 계산에 사용됩니다.
        /// </summary>
        [Tooltip("마지막 공격 시간 (Time.time 기준)")]
        protected float lastAttackTime;

        /// <summary>
        /// 무기 데이터로 무기를 초기화합니다.
        /// 무기가 생성된 후 또는 레벨 업 시 호출되어야 합니다.
        /// </summary>
        /// <param name="data">초기화에 사용할 WeaponData</param>
        public virtual void Initialize(WeaponData data)
        {
            // 무기 데이터 할당
            weaponData = data;
            
            // 마지막 공격 시간을 쿨다운 값의 음수로 설정하여 즉시 공격 가능하도록 함
            lastAttackTime = -data.cooldown;
        }

        /// <summary>
        /// 현재 공격이 가능한지 여부를 반환합니다.
        /// 공격 쿨다운이 지났는지 확인하는 데 사용됩니다.
        /// </summary>
        /// <returns>공격 쿨다운이 끝났으면 true, 그렇지 않으면 false</returns>
        public virtual bool CanAttack()
        {
            // 현재 시간과 마지막 공격 시간의 차이가 쿨다운보다 크거나 같은지 확인
            return Time.time - lastAttackTime >= weaponData.cooldown;
        }

        /// <summary>
        /// 지정된 방향으로 공격을 수행합니다.
        /// 이 메서드는 파생 클래스에서 반드시 구현해야 합니다.
        /// </summary>
        /// <param name="direction">공격 방향 (정규화된 벡터).
        /// 예: (0,1)은 위쪽, (1,0)은 오른쪽 방향을 나타냅니다.</param>
        /// <remarks>
        /// 이 메서드는 공격 애니메이션 시작, 투사체 발사, 충돌체 활성화 등을 포함할 수 있습니다.
        /// 공격이 성공적으로 시작되면 lastAttackTime을 현재 시간으로 업데이트해야 합니다.
        /// </remarks>
        public abstract void Attack(Vector2 direction);

        /// <summary>
        /// 공격 종료 시 호출되는 메서드입니다.
        /// 공격 애니메이션 종료 시나 공격 후 처리에 사용됩니다.
        /// </summary>
        /// <remarks>
        /// 이 메서드는 다음과 같은 상황에서 호출될 수 있습니다:
        /// - 공격 애니메이션이 완료되었을 때
        /// - 플레이어가 공격 버튼을 뗐을 때 (연사 무기인 경우)
        /// - 강제로 공격을 중단해야 할 때
        /// </remarks>
        public abstract void AttackEnd();

    }
}