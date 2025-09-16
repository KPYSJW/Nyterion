using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Puzzles
{
    public class FlowTileController
    {
        public int gridX { get; private set; }
        public int gridY { get; private set; }

        public TileType currentType { get; private set; }
        public BlockColor pathColor { get; private set; }

        public void Initialize(int x, int y)
        {
            gridX = x;
            gridY = y;
            ClearPath();
        }

        public void SetAsSensor(BlockColor color)
        {
            currentType = TileType.Sensor;
            pathColor = color;
        }

        public void SetAsPath(BlockColor color)
        {
            currentType = TileType.Path;
            pathColor = color;
        }

        public void ClearPath()
        {
            if (currentType == TileType.Path)
            {
                currentType = TileType.Empty;
            }
        }

        public bool IsAdjacent(FlowTileController other)
        {
            if (other == null) return false;

            int xDiff = UnityEngine.Mathf.Abs(gridX - other.gridX);
            int yDiff = UnityEngine.Mathf.Abs(gridY - other.gridY);

            return (xDiff == 1 && yDiff == 0) || (xDiff == 0 && yDiff == 1);
        }
    }
}