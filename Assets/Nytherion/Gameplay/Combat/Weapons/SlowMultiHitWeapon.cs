using UnityEngine;
using Nytherion.Data.ScriptableObjects.Weapons;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class SlowMultiHitWeapon : RangedWeapon
    {
        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            GameObject proj = Projectile(direction);

            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
        }
    }
}