using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using System;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 특정 무기(원거리/근거리)를 장착하고 있을 때 발동
    /// </summary>
    [Serializable]
    public class WeaponTypeCondition : RelicConditionBase
    {
        [Tooltip("요구하는 무기 타입 (예: Melee, Ranged)")]
        public WeaponType requiredWeaponType;

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            if (playerManager == null || playerManager.PlayerCombat == null) return false;

            var currentWeapon = playerManager.PlayerCombat.currentWeapon;
            if (currentWeapon == null || currentWeapon.weaponData == null) return false;

            return currentWeapon.weaponData.weaponType == requiredWeaponType;
        }
    }
}