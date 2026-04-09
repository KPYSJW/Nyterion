using UnityEngine;


namespace Nytherion.GamePlay.Combat.Weapon
{
    public class Bow : RangedWeapon
    {
        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack() || weaponData?.projectilePrefab == null || firePoint == null)
            {
                return;
            }

            try
            {
                Projectile(direction);
                lastAttackTime = Time.time;
            }
            catch (System.Exception)
            {
            }
        }

        public override void AttackEnd()
        {

        }
    }
}

