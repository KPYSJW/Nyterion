using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Synergy;

namespace Nytherion.GamePlay.Combat
{
public interface ISynergyEvaluator
{
    WeaponRelicSynergyData EvaluateSynergy(
            WeaponData weapon,
            List<RelicData> relics);
}
}