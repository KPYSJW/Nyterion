using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Engravings
{
    [CreateAssetMenu(fileName = "EngravingDatabase", menuName = "Engraving/Engraving Database")]
    public class EngravingDatabaseSO : ScriptableObject
    {
        public List<EngravingData> allEngravings;
    }
}