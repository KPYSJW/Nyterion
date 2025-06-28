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
        public int RotationState { get; private set; }
        public EngravingBlock(EngravingData data)
        {
            SourceData = data;
            baseLevel = data.level;
            RotationState = 0;
        }

        public void ChangeLevel(int amount)
        {
            SourceData.level += amount;
        }

        public void ResetLevel()
        {
            SourceData.level = baseLevel;
        }
        public void Rotate()
        {
            RotationState = (RotationState + 1) % 4;
        }
        public void SetRotationState(int newRotationState)
        {
            RotationState = newRotationState;
        }
        public List<InfluenceZone> GetRotatedInfluenceZones()
        {
            var rotatedZones = new List<InfluenceZone>();
            foreach (var zone in SourceData.influenceZones)
            {
                Vector2Int rotatedOffset = zone.offset;
                for (int i = 0; i < RotationState; i++)
                {
                    rotatedOffset = new Vector2Int(-rotatedOffset.y, rotatedOffset.x);
                }
                rotatedZones.Add(new InfluenceZone { offset = rotatedOffset, type = zone.type });
            }
            return rotatedZones;
        }
    }
}