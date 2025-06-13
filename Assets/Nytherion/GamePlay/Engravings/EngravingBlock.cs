using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Engravings
{
    public class EngravingBlock
    {
        public string BlockId { get; private set; }

        public List<Vector2Int> Shape { get; private set; }

        public string BlockType { get; private set; }

        public object Effect { get; set; }

        public EngravingBlock(string blockId, List<Vector2Int> shape, string blockType = "Default")
        {
            BlockId = blockId;
            Shape = shape ?? new List<Vector2Int> { Vector2Int.zero };
            BlockType = blockType;
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
            for (int i = 0; i < Shape.Count; i++)
            {
                var cell = Shape[i];
                Shape[i] = clockwise ? new Vector2Int(cell.y, -cell.x) : new Vector2Int(-cell.y, cell.x);
            }
        }
        public Vector2 GetVisualCenterPixelOffset(Vector2 cellSize, Vector2 spacing)
        {
            if (Shape == null || Shape.Count == 0)
            {
                return Vector2.zero;
            }

            float minX = 0, maxX = 0, minY = 0, maxY = 0;
            foreach (var offset in Shape)
            {
                minX = Mathf.Min(minX, offset.x);
                maxX = Mathf.Max(maxX, offset.x);
                minY = Mathf.Min(minY, offset.y);
                maxY = Mathf.Max(maxY, offset.y);
            }

            float centerX_InGridUnits = (minX + maxX) / 2.0f;
            float centerY_InGridUnits = (minY + maxY) / 2.0f;

            float pixelOffsetX = centerX_InGridUnits * (cellSize.x + spacing.x);
            float pixelOffsetY = -centerY_InGridUnits * (cellSize.y + spacing.y); 

            return new Vector2(pixelOffsetX, pixelOffsetY);
        }
    }
}
