using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Puzzle;
using Nytherion.UI.Components;
using Nytherion.Core.Systems;

namespace Nytherion.UI.Controllers
{
    public class PuzzleUIController : UIPanelBase
    {
        // UI 참조는 GameSceneUIRefs를 통해 주입받음
        private GameObject puzzlePanel;
        private PuzzleGridView puzzleGridView;
        private Button startButton;
        private Button resetButton;
        private Button exitButton;
        private TextMeshProUGUI attemptsText;
        private TextMeshProUGUI levelText;
        private TextMeshProUGUI statusText;

        // 결과창 관련 UI 요소들 - 추후 구현 예정으로 주석 처리
        /*
        [Header("Result UI")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultMessageText;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button exitResultButton;
        */

        private PuzzleManager puzzleManager;
        private GameSceneUIRefs uiRefs;

        [Inject]
        public void Construct(PuzzleManager puzzleManager, GameSceneUIRefs uiRefs)
        {
            this.puzzleManager = puzzleManager;
            this.uiRefs = uiRefs;

            // UI 참조 할당
            InitializeUIReferences();
        }

        /// <summary>
        /// GameSceneUIRefs에서 UI 참조들을 가져와서 할당
        /// </summary>
        private void InitializeUIReferences()
        {
            if (uiRefs == null)
            {
                Debug.LogError("[PuzzleUIController] GameSceneUIRefs not found!");
                return;
            }

            puzzlePanel = uiRefs.PuzzlePanel;
            puzzleGridView = uiRefs.PuzzleGridView;
            startButton = uiRefs.PuzzleStartButton;
            resetButton = uiRefs.PuzzleResetButton;
            exitButton = uiRefs.PuzzleExitButton;
            attemptsText = uiRefs.PuzzleAttemptsText;
            levelText = uiRefs.PuzzleLevelText;
            statusText = uiRefs.PuzzleStatusText;
        }

        private void Start()
        {
            SetupUI();
            SubscribeToEvents();
            UpdateUI();
        }

        private void SetupUI()
        {
            if (startButton != null)
                startButton.onClick.AddListener(OnStartButtonClicked);

            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetButtonClicked);

            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitButtonClicked);

            // 결과창 관련 버튼들 - 추후 구현 예정으로 주석 처리
            /*
            if (nextLevelButton != null)
                nextLevelButton.onClick.AddListener(OnNextLevelButtonClicked);

            if (retryButton != null)
                retryButton.onClick.AddListener(OnRetryButtonClicked);

            if (exitResultButton != null)
                exitResultButton.onClick.AddListener(OnExitResultButtonClicked);

            if (resultPanel != null)
                resultPanel.SetActive(false);
            */

            if (puzzlePanel != null)
                puzzlePanel.SetActive(false);
        }

        private void SubscribeToEvents()
        {
            if (puzzleManager != null)
            {
                puzzleManager.OnPuzzleStateChanged += OnPuzzleStateChanged;
                puzzleManager.OnAttemptsChanged += OnAttemptsChanged;
                // puzzleManager.OnTimeChanged += OnTimeChanged; // 시간 기능 제거
                puzzleManager.OnPuzzleCompleted += OnPuzzleCompleted;
                puzzleManager.OnPuzzleFailed += OnPuzzleFailed;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (puzzleManager != null)
            {
                puzzleManager.OnPuzzleStateChanged -= OnPuzzleStateChanged;
                puzzleManager.OnAttemptsChanged -= OnAttemptsChanged;
                // puzzleManager.OnTimeChanged -= OnTimeChanged; // 시간 기능 제거
                puzzleManager.OnPuzzleCompleted -= OnPuzzleCompleted;
                puzzleManager.OnPuzzleFailed -= OnPuzzleFailed;
            }
        }

        public void ShowPuzzle()
        {
            if (puzzlePanel != null)
                puzzlePanel.SetActive(true);

            if (puzzleGridView != null && puzzleManager.CurrentLevelData != null)
            {
                puzzleGridView.InitializeGrid(puzzleManager.CurrentLevelData);
            }

            UpdateUI();
        }

        public void HidePuzzle()
        {
            if (puzzlePanel != null)
                puzzlePanel.SetActive(false);

            // if (resultPanel != null)
            //     resultPanel.SetActive(false); // 결과창 기능 주석 처리
        }

        private void UpdateUI()
        {
            UpdateStatusUI();
            UpdateButtons();
        }

        private void UpdateStatusUI()
        {
            if (puzzleManager == null)
                return;

            if (attemptsText != null)
            {
                attemptsText.text = $"Attempts: {puzzleManager.RemainingAttempts}";
            }

            // 시간 표시 기능 주석 처리
            /*
            if (timeText != null)
            {
                float time = puzzleManager.TimeRemaining;
                if (time > 0)
                {
                    int minutes = Mathf.FloorToInt(time / 60);
                    int seconds = Mathf.FloorToInt(time % 60);
                    timeText.text = $"Time: {minutes:00}:{seconds:00}";
                }
                else
                {
                    timeText.text = "Time: --:--";
                }
            }
            */

            if (levelText != null)
            {
                levelText.text = $"Level: {puzzleManager.CurrentLevel + 1}/{puzzleManager.TotalLevels}";
            }

            if (statusText != null)
            {
                statusText.text = $"Status: {puzzleManager.CurrentState}";
            }
        }

        private void UpdateButtons()
        {
            if (puzzleManager == null)
                return;

            bool canStart = puzzleManager.CurrentState == PuzzleState.NotStarted;
            bool canReset = puzzleManager.CurrentState == PuzzleState.InProgress;

            if (startButton != null)
                startButton.interactable = canStart;

            if (resetButton != null)
                resetButton.interactable = canReset;
        }

        private void OnStartButtonClicked()
        {
            puzzleManager?.StartPuzzle();
        }

        private void OnResetButtonClicked()
        {
            puzzleManager?.ResetCurrentLevel();

            if (puzzleGridView != null && puzzleManager.CurrentLevelData != null)
            {
                puzzleGridView.InitializeGrid(puzzleManager.CurrentLevelData);
            }
        }

        private void OnExitButtonClicked()
        {
            HidePuzzle();
        }

        // 결과창 관련 버튼 핸들러들 - 추후 구현 예정으로 주석 처리
        /*
        private void OnNextLevelButtonClicked()
        {
            int nextLevel = puzzleManager.CurrentLevel + 1;
            if (nextLevel < puzzleManager.TotalLevels)
            {
                puzzleManager.LoadLevel(nextLevel);

                if (puzzleGridView != null && puzzleManager.CurrentLevelData != null)
                {
                    puzzleGridView.InitializeGrid(puzzleManager.CurrentLevelData);
                }
            }

            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        private void OnRetryButtonClicked()
        {
            puzzleManager?.ResetCurrentLevel();

            if (puzzleGridView != null && puzzleManager.CurrentLevelData != null)
            {
                puzzleGridView.InitializeGrid(puzzleManager.CurrentLevelData);
            }

            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        private void OnExitResultButtonClicked()
        {
            HidePuzzle();
        }
        */

        private void OnPuzzleStateChanged(PuzzleState newState)
        {
            UpdateUI();

            // 결과창 표시 기능 주석 처리 - 자동 리셋으로 대체
            /*
            if (newState == PuzzleState.Completed || newState == PuzzleState.Failed)
            {
                ShowResult();
            }
            */
        }

        private void OnAttemptsChanged(int remainingAttempts)
        {
            UpdateStatusUI();
        }

        // 시간 관련 이벤트 핸들러 주석 처리
        /*
        private void OnTimeChanged(float timeRemaining)
        {
            UpdateStatusUI();
        }
        */

        private void OnPuzzleCompleted()
        {
            Debug.Log("[PuzzleUIController] Puzzle completed!");
        }

        private void OnPuzzleFailed()
        {
            Debug.Log("[PuzzleUIController] Puzzle failed!");
        }

        // 결과창 표시 메서드 - 추후 구현 예정으로 주석 처리
        /*
        private void ShowResult()
        {
            if (resultPanel == null)
                return;

            resultPanel.SetActive(true);

            bool isCompleted = puzzleManager.CurrentState == PuzzleState.Completed;

            if (resultTitleText != null)
            {
                resultTitleText.text = isCompleted ? "Success!" : "Failed!";
            }

            if (resultMessageText != null)
            {
                if (isCompleted)
                {
                    resultMessageText.text = "Congratulations! You solved the puzzle!";
                }
                else
                {
                    resultMessageText.text = "You ran out of attempts. Try again!";
                }
            }

            if (nextLevelButton != null)
            {
                bool hasNextLevel = puzzleManager.CurrentLevel + 1 < puzzleManager.TotalLevels;
                nextLevelButton.gameObject.SetActive(isCompleted && hasNextLevel);
            }
        }
        */

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            if (startButton != null)
                startButton.onClick.RemoveListener(OnStartButtonClicked);
            if (resetButton != null)
                resetButton.onClick.RemoveListener(OnResetButtonClicked);
            if (exitButton != null)
                exitButton.onClick.RemoveListener(OnExitButtonClicked);

            // 결과창 관련 버튼 이벤트 리스너 제거 - 주석 처리
            /*
            if (nextLevelButton != null)
                nextLevelButton.onClick.RemoveListener(OnNextLevelButtonClicked);
            if (retryButton != null)
                retryButton.onClick.RemoveListener(OnRetryButtonClicked);
            if (exitResultButton != null)
                exitResultButton.onClick.RemoveListener(OnExitResultButtonClicked);
            */
        }
    }
}