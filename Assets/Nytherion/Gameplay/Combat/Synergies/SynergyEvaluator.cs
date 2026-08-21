using System.Collections.Generic;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Synergy;
using Nytherion.Core.Managers;
using VContainer;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class SynergyEvaluator :ISynergyEvaluator
    {
        private readonly List<WeaponRelicSynergyData> synergyTable;
        private EventManager eventManager;

        public SynergyEvaluator(List<WeaponRelicSynergyData> synergyDataList, EventManager eventManager)
        {
            this.synergyTable = synergyDataList;
            this.eventManager = eventManager;
        }

        public WeaponRelicSynergyData EvaluateSynergy(
            WeaponData weapon,
            List<RelicData> relics)
        {
            if(weapon == null || relics == null) return null;

            foreach (var relic in relics)
            {
                var match = synergyTable.FirstOrDefault(entry =>
                    entry.weaponName == weapon.weaponName &&
                    entry.relicName == relic.relicName);

                if (match != null)
                {
                    eventManager?.TriggerSynergyEvaluated(weapon, relic, match);
                    return match;
                }
            }

            eventManager?.TriggerSynergyEvaluated(weapon, null, null);
            return null;
        }
    }
}
