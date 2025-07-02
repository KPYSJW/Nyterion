using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Synergy;

namespace Nytherion.GamePlay.Combat
{
public interface ISynergyEvaluator
{
    WeaponEngravingSynergyData EvaluateSynergy(
            WeaponData weapon,
            List<EngravingData> engravings);
}
}