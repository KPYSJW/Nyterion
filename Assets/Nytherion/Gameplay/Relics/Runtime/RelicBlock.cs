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
        private bool influenceWarningIssued;
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
            if (!RelicInfluencePolicy.TryValidateInfluenceZones(SourceData.influenceZones, out string error))
            {
                if (!influenceWarningIssued)
                {
                    Debug.LogWarning($"[유물] {SourceData.name}: 영향 범위가 올바르지 않아 적용하지 않습니다. {error}", SourceData);
                    influenceWarningIssued = true;
                }
                return rotatedZones;
            }

            influenceWarningIssued = false;
            if (SourceData.influenceZones == null) return rotatedZones;
            foreach (var zone in SourceData.influenceZones)
            {
                if (zone == null || zone.type == InfluenceType.None) continue;
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
