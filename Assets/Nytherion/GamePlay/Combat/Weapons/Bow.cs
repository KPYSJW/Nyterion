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
                FireProjectiles(direction, 1, 15f);
                lastAttackTime = Time.time;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        public override void AttackEnd()
        {

        }
    }
}

