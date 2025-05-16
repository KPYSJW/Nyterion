using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Nytherion.Data.ScriptableObjects.Synergy
{
    [CreateAssetMenu(fileName = "NewSynergyData", menuName = "Data/Weapon-Engraving Synergy")]
    public class WeaponEngravingSynergyData : ScriptableObject
    {
        public string weaponName;
        public string engravingName;
        public bool overridesCursedPenalty;
        public float bonusAttackMultiplier = 1f;
        public float bonusCooldownMultiplier = 1f;
        public float bonusSpeedMultiplier = 1f;
    }
}
