using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Synergy;

namespace Nytherion.Gameplay.Combat
{
public interface ISynergyEvaluator
{
    WeaponEngravingSynergyData EvaluateSynergy(
            WeaponData weapon,
            List<EngravingData> engravings);
}
}