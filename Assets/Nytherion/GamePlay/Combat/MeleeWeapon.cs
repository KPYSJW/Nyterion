using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.Gameplay.Combat
{
    public class MeleeWeapon : WeaponBase
    {
        public override void Attack(Vector2 direction)
        {
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
    }
}
