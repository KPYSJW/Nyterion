using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Gameplay.Combat
{
    public class RangedWeapon : WeaponBase
    {
        public Transform firePoint;

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
    }
}