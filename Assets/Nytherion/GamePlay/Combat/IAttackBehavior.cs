using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 공격 행동을 정의하는 인터페이스입니다.
    /// 이 인터페이스를 구현하여 다양한 유형의 공격 행동을 정의할 수 있습니다.
    /// </summary>
    public interface IAttackBehavior
    {
        /// <summary>
        /// 대상에 대해 공격을 시도합니다.
        /// </summary>
        /// <param name="target">공격 대상의 Transform</param>
        /// <returns>공격이 성공적으로 수행되면 true, 그렇지 않으면 false</returns>
        bool TryAttack(Transform target);
        
        /// <summary>
        /// 대상이 공격 범위 내에 있는지 확인합니다.
        /// </summary>
        /// <param name="target">확인할 대상의 Transform</param>
        /// <returns>대상이 공격 범위 내에 있으면 true, 그렇지 않으면 false</returns>
        bool IsInAttackRange(Transform target);
        
        /// <summary>
        /// 현재 공격 쿨다운 상태를 가져옵니다.
        /// </summary>
        /// <value>0에서 1 사이의 값으로, 1이면 쿨다운이 완료된 상태입니다.</value>
        float AttackCoolDown { get; }
    }
}
