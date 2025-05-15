using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Weapons;

namespace Nytherion.Gameplay.Combat
{
    public abstract class WeaponBase : MonoBehaviour
    {
        protected WeaponData weaponData;

        protected float lastAttackTime;

        public virtual void Initialize(WeaponData data)
        {
            weaponData = data;
            lastAttackTime = -data.cooldown;
        }

        public virtual bool CanAttack()
        {
            return Time.time - lastAttackTime >= weaponData.cooldown;
        }

        public abstract void Attack(Vector2 direction);

    }
}