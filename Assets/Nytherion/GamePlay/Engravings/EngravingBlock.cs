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
        public string BlockType => SourceData.isCursed ? "Cursed" : "Normal";

        public EngravingBlock(EngravingData data)
        {
            SourceData = data;
        }

        public IEnumerable<Vector2Int> GetOccupiedCells(Vector2Int gridPosition)
        {
            foreach (var cell in Shape)
            {
                yield return gridPosition + cell;
            }
        }

        public void Rotate(bool clockwise = true)
        {
            for (int i = 0; i < SourceData.shape.Count; i++)
            {
                Vector2Int cell = SourceData.shape[i];
                SourceData.shape[i] = clockwise ? new Vector2Int(cell.y, -cell.x) : new Vector2Int(-cell.y, cell.x);
            }
        }

        public Vector2 GetVisualCenterPixelOffset(Vector2 cellSize, Vector2 spacing)
        {
            if (Shape == null || Shape.Count == 0) return Vector2.zero;

            float minX = 0, maxX = 0, minY = 0, maxY = 0;
            foreach (var offset in Shape)
            {
                minX = Mathf.Min(minX, offset.x);
                maxX = Mathf.Max(maxX, offset.x);
                minY = Mathf.Min(minY, offset.y);
                maxY = Mathf.Max(maxY, offset.y);
            }

            float centerX = (minX + maxX) / 2f;
            float centerY = (minY + maxY) / 2f;

            float offsetX = centerX * (cellSize.x + spacing.x);
            float offsetY = -centerY * (cellSize.y + spacing.y);

            return new Vector2(offsetX, offsetY);
        }
    }
}
