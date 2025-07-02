using UnityEngine;

namespace Nytherion.GamePlay.Dungeon
{
    public abstract class AbstractDungeonGenertor : MonoBehaviour
    {
        [SerializeField]
        protected TilemapVisualizer tilemapVisualizer;
        [SerializeField]
        protected Vector2Int startPosition = Vector2Int.zero;

        public void GenerateDungeon()
        {
            tilemapVisualizer.Clear();
            RunProceduralGeneration();
        }

        protected abstract void RunProceduralGeneration();

    }
}
