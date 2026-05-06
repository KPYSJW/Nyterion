using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Nytherion.Data.ScriptableObjects.Synergy
{
    [CreateAssetMenu(fileName = "NewSynergyData", menuName = "Data/Weapon-Relic Synergy")]
    public class WeaponRelicSynergyData : ScriptableObject
    {
        public string weaponName;
        public string relicName;
        public bool overridesCursedPenalty;
        public float bonusAttackMultiplier = 1f;
        public float bonusCooldownMultiplier = 1f;
        public float bonusSpeedMultiplier = 1f;
    }
}
