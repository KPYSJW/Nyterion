using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Weapons;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 모든 무기 클래스의 기본이 되는 추상 클래스입니다.
    /// 이 클래스를 상속받아 다양한 유형의 무기를 구현할 수 있습니다.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        /// <summary>
        /// 무기 데이터입니다. 이 데이터는 무기의 속성과 행동을 정의합니다.
        /// </summary>
        [SerializeField]public WeaponData weaponData;

        /// <summary>
        /// 마지막 공격 시간입니다. 이 시간은 공격 쿨다운을 계산하는 데 사용됩니다.
        /// </summary>
        protected float lastAttackTime;

        /// <summary>
        /// 무기 데이터로 무기를 초기화합니다.
        /// </summary>
        /// <param name="data">초기화에 사용할 WeaponData</param>
        public virtual void Initialize(WeaponData data)
        {
            weaponData = data;
            lastAttackTime = -data.cooldown;
        }

        /// <summary>
        /// 현재 공격이 가능한지 여부를 반환합니다.
        /// </summary>
        /// <returns>공격 쿨다운이 끝났으면 true, 그렇지 않으면 false</returns>
        public virtual bool CanAttack()
        {
            return Time.time - lastAttackTime >= weaponData.cooldown;
        }

        /// <summary>
        /// 지정된 방향으로 공격을 수행합니다.
        /// </summary>
        /// <param name="direction">공격 방향 (정규화된 벡터)</param>
        public abstract void Attack(Vector2 direction);

        /// <summary>
        /// 공격 종료 시 호출되는 메서드입니다.
        /// 공격 애니메이션 종료 시나 공격 후 처리에 사용됩니다.
        /// </summary>
        public abstract void AttackEnd();

    }
}