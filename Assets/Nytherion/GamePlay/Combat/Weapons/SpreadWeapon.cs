using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class SpreadWeapon : RangedWeapon
    {
        [Header("Spread Settings")]
        public int projectileCount = 3;
        public float spreadAngle = 30f;

        public override void Attack(Vector2 direction)
        {
            if (!CanAttack() || weaponData?.projectilePrefab == null || firePoint == null) return;
            
            if (projectileCount == 1)
            {
                Projectile(direction);
            }
            else
            {
                float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                float startAngle = baseAngle - (spreadAngle / 2f);
                float angleStep = spreadAngle / (projectileCount - 1);

                for (int i = 0; i < projectileCount; i++)
                {
                    float currentAngle = startAngle + (angleStep * i);
                    Vector2 spreadDirection = new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad));
                    Projectile(spreadDirection);
                }
            }
            lastAttackTime = Time.time;
        }
        public override void AttackEnd()
        {
        }
    }
}
