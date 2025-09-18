using UnityEngine;
using Nytherion.Core.Data;

namespace Nytherion.Data.ScriptableObjects.Puzzle
{
    [CreateAssetMenu(fileName = "PuzzleLevel", menuName = "Nytherion/Puzzle/Puzzle Level", order = 1)]
    public class PuzzleLevelSO : ScriptableObject
    {
        [Header("Puzzle Level Data")]
        public PuzzleLevelData levelData;

        [Header("Display Info")]
        public string levelName;
        [TextArea(3, 5)]
        public string description;

        private void OnValidate()
        {
            if (levelData != null && !string.IsNullOrEmpty(levelName))
            {
                name = $"Level_{levelName}_{levelData.gridWidth}x{levelData.gridHeight}";
            }
        }
    }
}