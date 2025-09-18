using UnityEngine;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Puzzle
{
    public class PuzzleTileController
    {
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public Vector2Int GridPosition => new Vector2Int(GridX, GridY);

        public TileType CurrentType { get; private set; }
        public PuzzleColor PathColor { get; private set; }

        public void Initialize(int x, int y)
        {
            GridX = x;
            GridY = y;
            ClearPath();
        }

        public void SetAsSensor(PuzzleColor color)
        {
            CurrentType = TileType.Sensor;
            PathColor = color;
        }

        public void SetAsPath(PuzzleColor color)
        {
            CurrentType = TileType.Path;
            PathColor = color;
        }

        public void ClearPath()
        {
            if (CurrentType == TileType.Path)
            {
                CurrentType = TileType.Empty;
                PathColor = PuzzleColor.Red; // Default color
            }
        }

        public bool IsEmpty => CurrentType == TileType.Empty;
        public bool IsSensor => CurrentType == TileType.Sensor;
        public bool IsPath => CurrentType == TileType.Path;
    }
}