using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Synergy;

namespace Nytherion.Gameplay.Combat
{
    public static class SynergyChecker
    {
        public static WeaponEngravingSynergyData CheckSynergy(
            WeaponData weapon,
            List<EngravingData> engravings,
            List<WeaponEngravingSynergyData> synergyTable)
        {
            foreach (var engraving in engravings)
            {
                var match = synergyTable.FirstOrDefault(entry =>
                    entry.weaponName == weapon.weaponName &&
                    entry.engravingName == engraving.engravingName);

                if (match != null)
                    return match;
            }

            return null; 
        }
    }
}
