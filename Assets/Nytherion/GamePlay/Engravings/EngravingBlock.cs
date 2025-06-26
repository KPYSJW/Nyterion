using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Engravings;

namespace Nytherion.GamePlay.Engravings
{
    public class EngravingBlock
    {
        public EngravingData SourceData { get; private set; }
        public string BlockId => SourceData.engravingName;
        public List<Vector2Int> Shape => SourceData.shape;
        
        private int baseLevel; 

        public EngravingBlock(EngravingData data)
        {
            SourceData = data;
            baseLevel = data.level; 
        }

        public void ChangeLevel(int amount)
        {
            SourceData.level += amount;
        }

        public void ResetLevel()
        {
            SourceData.level = baseLevel;
        }
    }
}