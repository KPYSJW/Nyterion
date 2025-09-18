using UnityEngine;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.UI.Controllers;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Puzzle
{
    /// <summary>
    /// 퍼즐 시스템 테스터
    /// F3: 퍼즐 시스템 테스트 (상태 확인 및 시작/리셋)
    /// F4: 퍼즐 UI 표시/숨김
    /// </summary>
    public class PuzzleSystemTester : MonoBehaviour
    {
        private PuzzleManager puzzleManager;
        private PuzzleUIController puzzleUIController;

        [Inject]
        public void Construct(PuzzleManager puzzleManager, PuzzleUIController puzzleUIController)
        {
            this.puzzleManager = puzzleManager;
            this.puzzleUIController = puzzleUIController;
        }

        private void Start()
        {
            CreateTestLevel();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                TestPuzzleSystem();
            }

            if (Input.GetKeyDown(KeyCode.F4))
            {
                ShowPuzzleUI();
            }
        }

        /// <summary>
        /// 테스트용 퍼즐 레벨 생성 (시간 제한 없음)
        /// 5x5 그리드, 시도 3회, Red/Blue 센서 페어
        /// </summary>
        private void CreateTestLevel()
        {
            var testLevel = new PuzzleLevelData
            {
                gridWidth = 5,
                gridHeight = 5,
                maxAttempts = 3,
                difficultyLevel = 1,
                sensorPairs = new List<PuzzleSensorData>
                {
                    new PuzzleSensorData
                    {
                        color = PuzzleColor.Red,
                        startPosition = new Vector2Int(0, 0),    // 좌상단
                        endPosition = new Vector2Int(4, 4)       // 우하단
                    },
                    new PuzzleSensorData
                    {
                        color = PuzzleColor.Blue,
                        startPosition = new Vector2Int(0, 4),    // 좌하단
                        endPosition = new Vector2Int(4, 0)       // 우상단
                    }
                }
            };

            if (puzzleManager != null)
            {
                Debug.Log("[PuzzleSystemTester] Test level created. Press F3 to test puzzle system, F4 to show UI.");
            }
        }

        private void TestPuzzleSystem()
        {
            if (puzzleManager == null)
            {
                Debug.LogError("[PuzzleSystemTester] PuzzleManager not found!");
                return;
            }

            Debug.Log($"[PuzzleSystemTester] Current puzzle state: {puzzleManager.CurrentState}");
            Debug.Log($"[PuzzleSystemTester] Remaining attempts: {puzzleManager.RemainingAttempts}");
            // 시간 제한 기능이 제거되어 시간 관련 로그 제거됨
            Debug.Log($"[PuzzleSystemTester] Current level: {puzzleManager.CurrentLevel + 1}/{puzzleManager.TotalLevels}");
            Debug.Log($"[PuzzleSystemTester] Consecutive failures: {puzzleManager.ConsecutiveFailures}");

            if (puzzleManager.CurrentState == PuzzleState.NotStarted)
            {
                Debug.Log("[PuzzleSystemTester] Starting puzzle...");
                puzzleManager.StartPuzzle();
            }
            else if (puzzleManager.CurrentState == PuzzleState.InProgress)
            {
                Debug.Log("[PuzzleSystemTester] Resetting puzzle...");
                puzzleManager.ResetCurrentLevel();
            }
        }

        private void ShowPuzzleUI()
        {
            if (puzzleUIController == null)
            {
                Debug.LogError("[PuzzleSystemTester] PuzzleUIController not found!");
                return;
            }

            Debug.Log("[PuzzleSystemTester] Showing puzzle UI...");
            puzzleUIController.ShowPuzzle();
        }
    }
}