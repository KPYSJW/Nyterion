using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Enums;

namespace Nytherion.Data.ScriptableObjects.Puzzles
{
    [Serializable]
    public class SensorPair
    {
        public Vector2Int startPosition;
        public Vector2Int endPosition;
        public BlockColor color;
    }

    [CreateAssetMenu(fileName = "New Puzzle Data", menuName = "Nytherion/Puzzle Data")]
    public class PuzzleData : ScriptableObject
    {
        [Header("Basic Info")]
        public string puzzleId;
        public string puzzleName;
        public PuzzleType puzzleType;

        [Header("Grid Settings")]
        public int gridWidth = 5;
        public int gridHeight = 5;

        [Header("Flow Puzzle Settings")]
        public List<SensorPair> sensorPairs = new List<SensorPair>();

        [Header("Difficulty")]
        [Range(1, 5)] public int difficultyLevel = 1;
        public int maxAttempts = 3;

        [Header("Rewards")]
        public int goldReward = 100;
        public int expReward = 50;

        private void OnValidate()
        {
            // puzzleId가 비어있으면 자동으로 생성
            if (string.IsNullOrEmpty(puzzleId))
            {
                puzzleId = name.Replace(" ", "_").ToLower();
            }

            // 센서 페어의 위치가 그리드 범위를 벗어나지 않도록 검증
            for (int i = 0; i < sensorPairs.Count; i++)
            {
                SensorPair pair = sensorPairs[i];

                // 시작 위치 검증
                pair.startPosition.x = Mathf.Clamp(pair.startPosition.x, 0, gridWidth - 1);
                pair.startPosition.y = Mathf.Clamp(pair.startPosition.y, 0, gridHeight - 1);

                // 끝 위치 검증
                pair.endPosition.x = Mathf.Clamp(pair.endPosition.x, 0, gridWidth - 1);
                pair.endPosition.y = Mathf.Clamp(pair.endPosition.y, 0, gridHeight - 1);
            }
        }

        /// <summary>
        /// 특정 위치에 센서가 있는지 확인
        /// </summary>
        public bool HasSensorAt(int x, int y)
        {
            foreach (var pair in sensorPairs)
            {
                if ((pair.startPosition.x == x && pair.startPosition.y == y) ||
                    (pair.endPosition.x == x && pair.endPosition.y == y))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 특정 위치의 센서 색상을 가져옴
        /// </summary>
        public BlockColor GetSensorColorAt(int x, int y)
        {
            foreach (var pair in sensorPairs)
            {
                if ((pair.startPosition.x == x && pair.startPosition.y == y) ||
                    (pair.endPosition.x == x && pair.endPosition.y == y))
                {
                    return pair.color;
                }
            }
            return BlockColor.Red; // 기본값
        }

        /// <summary>
        /// 특정 색상의 센서 페어를 가져옴
        /// </summary>
        public SensorPair GetSensorPairByColor(BlockColor color)
        {
            foreach (var pair in sensorPairs)
            {
                if (pair.color == color)
                {
                    return pair;
                }
            }
            return null;
        }
    }
}