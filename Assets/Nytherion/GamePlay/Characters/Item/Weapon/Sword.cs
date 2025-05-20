using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Nytherion.GamePlay.Combat.Weapon
{
    public class Sword : MeleeWeapon
    {
        [Header("Melee Settings")]
        [Tooltip("무기의 시각적 표현을 담당하는 스프라이트 렌더러")]
        public SpriteRenderer sprite;


        /// 지정된 방향으로 근접 공격을 수행합니다.
        /// 공격 범위 내의 모든 대상에게 데미지를 입힙니다.
        /// </summary>
        /// <param name="direction">공격 방향 (현재는 사용되지 않음)</param>
        public override void Attack(Vector2 direction)
        {


            // 공격 쿨다운 확인
            if (!CanAttack())
            {
                return;
            }
           
            StartCoroutine(SwordAttack());

            // 마지막 공격 시간 갱신
            lastAttackTime = Time.time;
        }


        public override void AttackEnd()
        {
            Debug.Log("공격종료");
            Collider(false);
        }

        private IEnumerator SwordAttack()
        {
            Collider(true);
            float duration = 0.3f;
            float elapsed = 0f;
            float startAngle = transform.localEulerAngles.z;
            float EndAngle = startAngle + 180f;

            Quaternion initialRotation=transform.rotation;
            Quaternion targetRotation = Quaternion.Euler(0, 0, EndAngle);

            while(elapsed < duration)
            {
                transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, elapsed/duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
         
            AttackEnd();
            transform.rotation = initialRotation;
        }
    }
}
