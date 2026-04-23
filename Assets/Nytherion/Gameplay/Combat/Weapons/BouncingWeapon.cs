using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class BouncingWeapon : RangedWeapon
    {
        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            lastAttackTime = Time.time;

            FireProjectiles(direction, 1, 15f);
        }

        public override void AttackEnd()
        {
        }
    }
}