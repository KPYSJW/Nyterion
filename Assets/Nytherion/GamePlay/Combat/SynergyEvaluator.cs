using System.Collections.Generic;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Synergy;
using Nytherion.Core.Managers;
using VContainer;

namespace Nytherion.GamePlay.Combat
{
    public class SynergyEvaluator :ISynergyEvaluator
    {
        private readonly List<WeaponEngravingSynergyData> synergyTable;
        private EventManager eventManager;

        [Inject]
        public void Construct(EventManager eventManager)
        {
            this.eventManager = eventManager;
        }

        public SynergyEvaluator(List<WeaponEngravingSynergyData> synergyDataList)
        {
            synergyTable = synergyDataList;
        }

        public WeaponEngravingSynergyData EvaluateSynergy(
            WeaponData weapon,
            List<EngravingData> engravings)
        {
            foreach (var engraving in engravings)
            {
                var match = synergyTable.FirstOrDefault(entry =>
                    entry.weaponName == weapon.weaponName &&
                    entry.engravingName == engraving.engravingName);

                if (match != null)
                {
                    eventManager.TriggerSynergyEvaluated(weapon, engraving, match);
                    return match;
                }
            }

            eventManager.TriggerSynergyEvaluated(weapon, null, null);
            return null;
        }
    }
}
