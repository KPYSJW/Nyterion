using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace Nytherion.Combat
{
    public class RangedWeapon : WeaponBase
    {
        public Transform firePoint;
        public SpriteRenderer sprite;
        public override void Attack(Vector2 direction)
        {
            if (!CanAttack() || weaponData.projectilePrefab == null) return;

            GameObject projectile = Instantiate(weaponData.projectilePrefab, firePoint.position, Quaternion.identity);
            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction.normalized * 8f;
            }

            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
            sprite.color = new Color(1, 1, 1, 1);
        }
    }
}