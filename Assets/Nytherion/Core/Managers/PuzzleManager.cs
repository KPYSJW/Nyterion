using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;

namespace Nytherion.Core.Managers
{
    /// <summary>
    /// 퍼즐 시스템 관리자
    /// - 시도 횟수 기반 퍼즐 (시간 제한 없음)
    /// - 패스 그리기 방식: 색상별 센서를 연결하는 경로 생성
    /// - 연속 실패 시 난이도 낮춤 기능
    /// - 자동 리셋 및 저장/로드 지원
    /// </summary>
    public class PuzzleManager : BaseManager, ISaveable
    {
        [Header("Puzzle Settings")]
        [SerializeField] private List<PuzzleLevelData> puzzleLevels = new List<PuzzleLevelData>(); // 퍼즐 레벨 데이터 목록
        [SerializeField] private int currentLevelIndex = 0; // 현재 플레이 중인 레벨 인덱스
        [SerializeField] private float autoResetDelay = 1.0f; // 실패 후 자동 리셋 대기 시간 (초)
        [SerializeField] private int maxConsecutiveFailures = 3; // 연속 실패 후 난이도 낮춤 제안 임계값

        // 퍼즐 이벤트 시스템
        public event Action<PuzzleState> OnPuzzleStateChanged;      // 퍼즐 상태 변경 (시작/진행중/완료/실패)
        public event Action<int> OnAttemptsChanged;                 // 남은 시도 횟수 변경
        public event Action<PuzzleColor, List<Vector2Int>> OnPathCompleted; // 패스 완성 (색상, 경로 좌표)
        public event Action<PuzzleColor> OnPathCleared;             // 패스 지우기
        public event Action OnPuzzleCompleted;                      // 퍼즐 완전 클리어
        public event Action OnPuzzleFailed;                         // 퍼즐 실패 (시도 횟수 소진)
        public event Action OnDifficultyDownOffered;                // 연속 실패 시 난이도 낮춤 제안

        // 퍼즐 게임 상태
        private PuzzleGameState gameState = new PuzzleGameState();   // 현재 게임 상태 (시도 횟수, 완성된 패스 등)
        private PuzzleLevelData currentLevel;                        // 현재 로드된 레벨 데이터
        private bool isGameActive = false;                           // 게임 진행 중 여부
        private int consecutiveFailures = 0;                         // 연속 실패 횟수 추적
        private bool waitingForDifficultyChoice = false;             // 난이도 낮춤 선택 UI 대기 중

        // 의존성 주입
        private EventManager eventManager;

        [Inject]
        public void Construct(EventManager eventManager)
        {
            this.eventManager = eventManager;
        }


        protected override void OnInitializeInternal()
        {
            if (puzzleLevels.Count > 0)
            {
                LoadLevel(0);
            }
        }

        #region Level Management

        /// <summary>
        /// 퍼즐 레벨 로드 및 초기화
        /// </summary>
        /// <param name="levelIndex">로드할 레벨 인덱스 (0부터 시작)</param>
        public void LoadLevel(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= puzzleLevels.Count)
            {
                Debug.LogError($"[PuzzleManager] Invalid level index: {levelIndex}");
                return;
            }

            currentLevelIndex = levelIndex;
            currentLevel = puzzleLevels[levelIndex];
            gameState.Reset();
            gameState.currentLevel = levelIndex;
            gameState.remainingAttempts = currentLevel.maxAttempts;

            SetPuzzleState(PuzzleState.NotStarted);
            Debug.Log($"[PuzzleManager] Loaded level {levelIndex + 1}");
        }

        /// <summary>
        /// 퍼즐 시작 - 게임 상태를 InProgress로 변경하고 입력 활성화
        /// </summary>
        public void StartPuzzle()
        {
            if (currentLevel == null)
            {
                Debug.LogError("[PuzzleManager] No level loaded");
                return;
            }

            SetPuzzleState(PuzzleState.InProgress);
            isGameActive = true;

            Debug.Log($"[PuzzleManager] Started puzzle with {gameState.remainingAttempts} attempts");
        }

        /// <summary>
        /// 현재 레벨 리셋 - 시도 횟수 완전 복구 (리셋 버튼용)
        /// </summary>
        public void ResetCurrentLevel()
        {
            if (currentLevel != null)
            {
                // 리셋 버튼: 퍼즐 원상복구 + 횟수 회복
                gameState.Reset();
                gameState.currentLevel = currentLevelIndex;
                gameState.remainingAttempts = currentLevel.maxAttempts;

                SetPuzzleState(PuzzleState.NotStarted);
                waitingForDifficultyChoice = false;

                Debug.Log($"[PuzzleManager] Reset level {currentLevelIndex + 1} with full attempts restored");
            }
        }

        #endregion

        #region Game State Management

        /// <summary>
        /// 퍼즐 상태 변경 및 이벤트 발생
        /// </summary>
        /// <param name="newState">새로운 퍼즐 상태</param>
        private void SetPuzzleState(PuzzleState newState)
        {
            if (gameState.state != newState)
            {
                gameState.state = newState;
                OnPuzzleStateChanged?.Invoke(newState);

                switch (newState)
                {
                    case PuzzleState.Completed:
                        isGameActive = false;
                        consecutiveFailures = 0; // 성공 시 연속 실패 카운터 리셋
                        OnPuzzleCompleted?.Invoke();
                        break;
                    case PuzzleState.Failed:
                        isGameActive = false;
                        consecutiveFailures++;
                        OnPuzzleFailed?.Invoke();

                        // 자동 리셋 또는 난이도 낮춤 제안
                        StartCoroutine(HandleFailure());
                        break;
                }
            }
        }

        /// <summary>
        /// 시도 횟수 소모 - 실패한 패스 그리기 시 호출
        /// </summary>
        private void ConsumeAttempt()
        {
            if (gameState.remainingAttempts > 0)
            {
                gameState.remainingAttempts--;
                OnAttemptsChanged?.Invoke(gameState.remainingAttempts);

                if (gameState.remainingAttempts <= 0)
                {
                    SetPuzzleState(PuzzleState.Failed);
                }
            }
        }

        private IEnumerator HandleFailure()
        {
            yield return new WaitForSeconds(autoResetDelay);

            if (consecutiveFailures >= maxConsecutiveFailures && !waitingForDifficultyChoice)
            {
                // 연속 실패 시 난이도 낮춤 제안
                waitingForDifficultyChoice = true;
                OnDifficultyDownOffered?.Invoke();
                Debug.Log($"[PuzzleManager] {consecutiveFailures} consecutive failures - offering difficulty reduction");
            }
            else
            {
                // 일반 자동 리셋
                AutoResetLevel();
            }
        }

        private void AutoResetLevel()
        {
            if (currentLevel != null)
            {
                gameState.Reset();
                gameState.currentLevel = currentLevelIndex;
                gameState.remainingAttempts = currentLevel.maxAttempts;

                SetPuzzleState(PuzzleState.NotStarted);
                Debug.Log("[PuzzleManager] Auto-reset completed");
            }
        }

        public void AcceptDifficultyDown()
        {
            if (waitingForDifficultyChoice)
            {
                // 횟수 1회 추가 (난이도 낮춤)
                int bonusAttempts = currentLevel.maxAttempts + 1;
                gameState.Reset();
                gameState.currentLevel = currentLevelIndex;
                gameState.remainingAttempts = bonusAttempts;

                consecutiveFailures = 0;
                waitingForDifficultyChoice = false;

                SetPuzzleState(PuzzleState.NotStarted);
                Debug.Log($"[PuzzleManager] Difficulty reduced - bonus attempts: {bonusAttempts}");
            }
        }

        public void DeclineDifficultyDown()
        {
            if (waitingForDifficultyChoice)
            {
                waitingForDifficultyChoice = false;
                AutoResetLevel();
                Debug.Log("[PuzzleManager] Difficulty reduction declined - normal reset");
            }
        }

        #endregion

        #region Path Management

        /// <summary>
        /// 패스 완성 시도 - 유효한 패스인지 검증하고 완성 처리
        /// </summary>
        /// <param name="color">패스 색상</param>
        /// <param name="pathPositions">패스 경로 좌표들</param>
        /// <returns>패스 완성 성공 여부</returns>
        public bool TryCompletePath(PuzzleColor color, List<Vector2Int> pathPositions)
        {
            if (!isGameActive || currentLevel == null)
                return false;

            // Validate path
            if (!IsValidPath(color, pathPositions))
            {
                ConsumeAttempt();
                return false;
            }

            // Store completed path
            gameState.completedPaths[color] = new List<Vector2Int>(pathPositions);
            OnPathCompleted?.Invoke(color, pathPositions);

            // Check for puzzle completion
            CheckPuzzleCompletion();

            return true;
        }

        /// <summary>
        /// 완성된 패스 지우기
        /// </summary>
        /// <param name="color">지울 패스의 색상</param>
        public void ClearPath(PuzzleColor color)
        {
            if (gameState.completedPaths.ContainsKey(color))
            {
                gameState.completedPaths.Remove(color);
                OnPathCleared?.Invoke(color);
            }
        }

        /// <summary>
        /// 패스 유효성 검증
        /// - 최소 2개 이상의 좌표
        /// - 해당 색상의 센서 쌍과 정확히 연결
        /// - 연속된 경로 (인접한 타일들로만 구성)
        /// </summary>
        private bool IsValidPath(PuzzleColor color, List<Vector2Int> pathPositions)
        {
            if (pathPositions.Count < 2)
                return false;

            // Find sensor pair for this color
            var sensorPair = currentLevel.sensorPairs.FirstOrDefault(pair => pair.color == color);
            if (sensorPair == null)
                return false;

            // Check if path connects the sensors
            Vector2Int start = pathPositions[0];
            Vector2Int end = pathPositions[pathPositions.Count - 1];

            bool validConnection = (start == sensorPair.startPosition && end == sensorPair.endPosition) ||
                                   (start == sensorPair.endPosition && end == sensorPair.startPosition);

            if (!validConnection)
                return false;

            // Check if path is continuous
            for (int i = 1; i < pathPositions.Count; i++)
            {
                if (!AreAdjacent(pathPositions[i - 1], pathPositions[i]))
                    return false;
            }

            return true;
        }

        private bool AreAdjacent(Vector2Int pos1, Vector2Int pos2)
        {
            return Mathf.Abs(pos1.x - pos2.x) + Mathf.Abs(pos1.y - pos2.y) == 1;
        }

        private void CheckPuzzleCompletion()
        {
            if (gameState.completedPaths.Count != currentLevel.sensorPairs.Count)
                return;

            // Check if all tiles are filled
            int totalTiles = currentLevel.gridWidth * currentLevel.gridHeight;
            int filledTiles = gameState.completedPaths.Values.Sum(path => path.Count);
            filledTiles -= gameState.completedPaths.Count; // Subtract sensor tiles

            if (filledTiles == totalTiles)
            {
                SetPuzzleState(PuzzleState.Completed);
            }
        }

        #endregion

        // Time Management 기능 제거됨
        /*
        #region Time Management

        private void Update()
        {
            if (isGameActive && currentLevel.timeLimit > 0)
            {
                gameState.timeRemaining -= Time.deltaTime;
                OnTimeChanged?.Invoke(gameState.timeRemaining);

                if (gameState.timeRemaining <= 0)
                {
                    SetPuzzleState(PuzzleState.Failed);
                }
            }
        }


        #endregion
        */

        #region Public Getters

        public PuzzleState CurrentState => gameState.state;
        public int RemainingAttempts => gameState.remainingAttempts;
        public int CurrentLevel => gameState.currentLevel;
        public PuzzleLevelData CurrentLevelData => currentLevel;
        public Dictionary<PuzzleColor, List<Vector2Int>> CompletedPaths => gameState.completedPaths;
        public bool IsGameActive => isGameActive;
        public int TotalLevels => puzzleLevels.Count;
        public int ConsecutiveFailures => consecutiveFailures; // 연속 실패 횟수
        public bool IsWaitingForDifficultyChoice => waitingForDifficultyChoice; // 난이도 선택 대기 중

        #endregion

        #region Save/Load System

        public override void PopulateSaveData(SaveData saveData)
        {
            saveData.puzzleCurrentLevel = currentLevelIndex;
            saveData.puzzleState = (int)gameState.state;
            saveData.puzzleRemainingAttempts = gameState.remainingAttempts;
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            currentLevelIndex = saveData.puzzleCurrentLevel;
            gameState.state = (PuzzleState)saveData.puzzleState;
            gameState.remainingAttempts = saveData.puzzleRemainingAttempts;

            if (puzzleLevels.Count > currentLevelIndex)
            {
                currentLevel = puzzleLevels[currentLevelIndex];
                isGameActive = gameState.state == PuzzleState.InProgress;
            }
        }

        #endregion

        public override string GetStatusInfo()
        {
            return $"{base.GetStatusInfo()}, Level: {currentLevelIndex + 1}/{puzzleLevels.Count}, State: {gameState.state}, Attempts: {gameState.remainingAttempts}";
        }
    }
}