using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Relics
{
    [CreateAssetMenu(fileName = "RelicDatabase", menuName = "Relic/Relic Database")]
    public class RelicDatabaseSO : ScriptableObject
    {
        public List<RelicData> allRelics;
    }
}