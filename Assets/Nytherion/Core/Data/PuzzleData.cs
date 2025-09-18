using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class PuzzleSensorData
    {
        public PuzzleColor color;
        public Vector2Int startPosition;
        public Vector2Int endPosition;
    }

    [Serializable]
    public class PuzzleLevelData
    {
        [Header("Grid Settings")]
        public int gridWidth = 5;
        public int gridHeight = 5;

        [Header("Sensor Pairs")]
        public List<PuzzleSensorData> sensorPairs = new List<PuzzleSensorData>();

        [Header("Difficulty Settings")]
        public int maxAttempts = 3;
        public int difficultyLevel = 1;
    }

    [Serializable]
    public class PuzzleGameState
    {
        public PuzzleState state = PuzzleState.NotStarted;
        public int remainingAttempts;
        public int currentLevel;
        public Dictionary<PuzzleColor, List<Vector2Int>> completedPaths = new Dictionary<PuzzleColor, List<Vector2Int>>();

        public PuzzleGameState()
        {
            Reset();
        }

        public void Reset()
        {
            state = PuzzleState.NotStarted;
            remainingAttempts = 0;
            currentLevel = 0;
            completedPaths.Clear();
        }
    }
}