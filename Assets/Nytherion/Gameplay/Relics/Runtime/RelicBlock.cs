using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Relics;

namespace Nytherion.GamePlay.Relics
{
    public class RelicBlock
    {
        public const int MaxLevel = 7;

        public RelicData SourceData { get; private set; }
        public string BlockId { get; private set; }
        public string RelicId => SourceData.relicName;
        public List<Vector2Int> Shape => SourceData.shape;
        public int Level => SourceData.level;

        private int baseLevel;
        public int RotationState { get; private set; }

        public RelicBlock(RelicData data)
        {
            SourceData = data;
            BlockId = System.Guid.NewGuid().ToString();
            baseLevel = Mathf.Min(data.level, MaxLevel);
            SourceData.level = baseLevel;
            RotationState = 0;
        }

        public RelicBlock(RelicData data, int level)
        {
            SourceData = data;
            BlockId = System.Guid.NewGuid().ToString();
            baseLevel = Mathf.Min(level, MaxLevel);
            SourceData.level = baseLevel;
            RotationState = 0;
        }

        public void ChangeLevel(int amount)
        {
            long changedLevel = (long)SourceData.level + amount;
            SourceData.level = (int)System.Math.Max(int.MinValue, System.Math.Min(changedLevel, MaxLevel));
        }

        public bool IncreaseBaseLevel()
        {
            if (baseLevel >= MaxLevel) return false;

            baseLevel++;
            SourceData.level = Mathf.Min(SourceData.level + 1, MaxLevel);
            return true;
        }

        public void SetDisabled(bool disabled)
        {
            SourceData.isDisabled = disabled;
        }

        public void ResetLevel()
        {
            SourceData.level = baseLevel;
            SourceData.isDisabled = false;
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
                rotatedZones.Add(new InfluenceZone
                {
                    offset = rotatedOffset,
                    type = zone.type,
                    levelAmount = zone.GetLevelAmount()
                });
            }
            return rotatedZones;
        }
    }
}
