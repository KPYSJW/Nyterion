using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Interfaces;

namespace Nytherion.Combat
{
    public class MeleeWeapon : WeaponBase
    {
        public SpriteRenderer sprite;
        public override void Attack(Vector2 direction)
        {
            Debug.Log("АјАн");
            sprite.color = Color.red;
            if (!CanAttack()) return;

            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, weaponData.range, Vector2.zero);

            foreach (RaycastHit2D hit in hits)
            {
                var target = hit.collider.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(weaponData.damage);
                }
            }

            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
            sprite.color = new Color(1, 1, 1, 1);
        }
    }
}
