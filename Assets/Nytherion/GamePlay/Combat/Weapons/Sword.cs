using System.Collections;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class Sword : MeleeWeapon
    {
        [Header("Melee Settings")]
        public SpriteRenderer sprite;

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
        
            if (!CanAttack())
            {
               
                return;
            }
           
            StartCoroutine(SwordAttack());

            lastAttackTime = Time.time;
        }


        public override void AttackEnd()
        {
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
