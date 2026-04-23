using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class SpreadWeapon : RangedWeapon
    {
        [Header("Spread Settings")]
        public int projectileCount = 3;
        public float spreadAngle = 30f;

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack() || weaponData?.projectilePrefab == null || firePoint == null) return;

            FireProjectiles(direction, projectileCount, spreadAngle);

            lastAttackTime = Time.time;
        }        public override void AttackEnd()
        {
        }
    }
}
