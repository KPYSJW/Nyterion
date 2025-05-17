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
        [Tooltip("������ �ð��� ǥ���� ����ϴ� ��������Ʈ ������")]
        public SpriteRenderer sprite;


        /// ������ �������� ���� ������ �����մϴ�.
        /// ���� ���� ���� ��� ��󿡰� �������� �����ϴ�.
        /// </summary>
        /// <param name="direction">���� ���� (����� ������ ����)</param>
        public override void Attack(Vector2 direction)
        {


            // ���� ��ٿ� Ȯ��
            if (!CanAttack())
            {
                return;
            }
           
            StartCoroutine(SwordAttack());

            // ������ ���� �ð� ����
            lastAttackTime = Time.time;
        }


        public override void AttackEnd()
        {
            Debug.Log("��������");
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
