using UnityEngine;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Puzzles;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Puzzles
{
    /// <summary>
    /// 퍼즐 시스템 테스트를 위한 헬퍼 클래스
    /// </summary>
    public class PuzzleTestHelper : MonoBehaviour
    {
        [Header("테스트 설정")]
        [SerializeField] private bool runTestOnStart = false;
        [SerializeField] private FlowPuzzleManager flowPuzzleManager;

        private PuzzleManager _puzzleManager;
        private EventManager _eventManager;

        [Inject]
        public void Construct(PuzzleManager puzzleManager, EventManager eventManager)
        {
            _puzzleManager = puzzleManager;
            _eventManager = eventManager;
        }

        private void Start()
        {
            if (runTestOnStart)
            {
                TestPuzzleSystem();
            }
        }

        [ContextMenu("퍼즐 시스템 테스트")]
        public void TestPuzzleSystem()
        {
            Debug.Log("<color=cyan>[PuzzleTestHelper] 퍼즐 시스템 테스트 시작</color>");

            // 1. PuzzleManager 연결 테스트
            TestPuzzleManagerIntegration();

            // 2. 간단한 퍼즐 데이터 생성 및 테스트
            TestSimplePuzzleCreation();

            // 3. FlowPuzzleManager 테스트
            TestFlowPuzzleManager();
        }

        private void TestPuzzleManagerIntegration()
        {
            Debug.Log("[PuzzleTestHelper] 1. PuzzleManager 통합 테스트");

            if (_puzzleManager == null)
            {
                Debug.LogError("[PuzzleTestHelper] PuzzleManager가 주입되지 않았습니다!");
                return;
            }

            // 테스트 퍼즐 등록
            string testPuzzleId = "test_flow_puzzle_001";
            _puzzleManager.RegisterPuzzle(testPuzzleId, PuzzleType.FlowConnection, 3);

            // 상태 확인
            bool canAttempt = _puzzleManager.CanAttemptPuzzle(testPuzzleId);
            int remainingAttempts = _puzzleManager.GetRemainingAttempts(testPuzzleId);

            Debug.Log($"[PuzzleTestHelper] 퍼즐 등록 완료: {testPuzzleId}");
            Debug.Log($"[PuzzleTestHelper] 시도 가능: {canAttempt}, 남은 시도: {remainingAttempts}");

            // 시도 테스트
            if (canAttempt)
            {
                bool used = _puzzleManager.UseAttempt(testPuzzleId);
                int newRemaining = _puzzleManager.GetRemainingAttempts(testPuzzleId);
                Debug.Log($"[PuzzleTestHelper] 시도 사용: {used}, 새로운 남은 시도: {newRemaining}");
            }
        }

        private void TestSimplePuzzleCreation()
        {
            Debug.Log("[PuzzleTestHelper] 2. 간단한 퍼즐 데이터 생성 테스트");

            // 런타임에서 간단한 퍼즐 데이터 생성
            PuzzleData testPuzzle = CreateSimpleTestPuzzle();

            if (testPuzzle != null)
            {
                Debug.Log($"[PuzzleTestHelper] 테스트 퍼즐 생성 완료: {testPuzzle.puzzleId}");
                Debug.Log($"[PuzzleTestHelper] 그리드 크기: {testPuzzle.gridWidth}x{testPuzzle.gridHeight}");
                Debug.Log($"[PuzzleTestHelper] 센서 페어 수: {testPuzzle.sensorPairs.Count}");
            }
        }

        private void TestFlowPuzzleManager()
        {
            Debug.Log("[PuzzleTestHelper] 3. FlowPuzzleManager 테스트");

            if (flowPuzzleManager == null)
            {
                Debug.LogWarning("[PuzzleTestHelper] FlowPuzzleManager가 할당되지 않았습니다.");
                return;
            }

            // 간단한 퍼즐 데이터로 초기화 테스트
            PuzzleData testPuzzle = CreateSimpleTestPuzzle();
            if (testPuzzle != null)
            {
                flowPuzzleManager.InitializePuzzle(testPuzzle);
                Debug.Log("[PuzzleTestHelper] FlowPuzzleManager 초기화 완료");
            }
        }

        private PuzzleData CreateSimpleTestPuzzle()
        {
            // ScriptableObject 인스턴스 생성 (런타임용)
            PuzzleData puzzle = ScriptableObject.CreateInstance<PuzzleData>();

            puzzle.puzzleId = "runtime_test_puzzle";
            puzzle.puzzleName = "테스트 Flow 퍼즐";
            puzzle.puzzleType = PuzzleType.FlowConnection;
            puzzle.gridWidth = 5;
            puzzle.gridHeight = 5;
            puzzle.difficultyLevel = 1;
            puzzle.maxAttempts = 3;
            puzzle.goldReward = 100;
            puzzle.expReward = 50;

            // 간단한 센서 페어 추가 (빨간색)
            puzzle.sensorPairs = new List<SensorPair>
            {
                new SensorPair
                {
                    startPosition = new Vector2Int(0, 0),
                    endPosition = new Vector2Int(4, 4),
                    color = BlockColor.Red
                },
                new SensorPair
                {
                    startPosition = new Vector2Int(0, 4),
                    endPosition = new Vector2Int(4, 0),
                    color = BlockColor.Blue
                }
            };

            return puzzle;
        }

        [ContextMenu("퍼즐 시도 테스트")]
        public void TestPuzzleAttempt()
        {
            if (_puzzleManager == null) return;

            string testId = "test_flow_puzzle_001";

            Debug.Log($"[PuzzleTestHelper] 퍼즐 시도 테스트: {testId}");
            Debug.Log($"[PuzzleTestHelper] 시도 전 상태 - 남은 시도: {_puzzleManager.GetRemainingAttempts(testId)}");

            bool canAttempt = _puzzleManager.CanAttemptPuzzle(testId);
            if (canAttempt)
            {
                _puzzleManager.UseAttempt(testId);
                Debug.Log($"[PuzzleTestHelper] 시도 후 상태 - 남은 시도: {_puzzleManager.GetRemainingAttempts(testId)}");
            }
            else
            {
                Debug.Log("[PuzzleTestHelper] 더 이상 시도할 수 없습니다.");
            }
        }

        [ContextMenu("퍼즐 완료 테스트")]
        public void TestPuzzleCompletion()
        {
            if (_puzzleManager == null) return;

            string testId = "test_flow_puzzle_001";

            Debug.Log($"[PuzzleTestHelper] 퍼즐 완료 테스트: {testId}");
            _puzzleManager.CompletePuzzle(testId);

            bool isCompleted = _puzzleManager.IsPuzzleCompleted(testId);
            Debug.Log($"[PuzzleTestHelper] 완료 상태: {isCompleted}");
        }

        [ContextMenu("퍼즐 리셋 테스트")]
        public void TestPuzzleReset()
        {
            if (_puzzleManager == null) return;

            string testId = "test_flow_puzzle_001";

            Debug.Log($"[PuzzleTestHelper] 퍼즐 리셋 테스트: {testId}");
            _puzzleManager.ResetPuzzle(testId);

            int remainingAttempts = _puzzleManager.GetRemainingAttempts(testId);
            bool isCompleted = _puzzleManager.IsPuzzleCompleted(testId);
            bool isFailed = _puzzleManager.IsPuzzleFailed(testId);

            Debug.Log($"[PuzzleTestHelper] 리셋 후 상태 - 남은 시도: {remainingAttempts}, 완료: {isCompleted}, 실패: {isFailed}");
        }

        private void OnEnable()
        {
            // 퍼즐 이벤트 구독
            if (_puzzleManager != null)
            {
                _puzzleManager.OnAttemptUsed += OnPuzzleAttemptUsed;
                _puzzleManager.OnPuzzleCompleted += OnPuzzleCompleted;
                _puzzleManager.OnPuzzleFailed += OnPuzzleFailed;
                _puzzleManager.OnPuzzleReset += OnPuzzleReset;
            }
        }

        private void OnDisable()
        {
            // 퍼즐 이벤트 구독 해제
            if (_puzzleManager != null)
            {
                _puzzleManager.OnAttemptUsed -= OnPuzzleAttemptUsed;
                _puzzleManager.OnPuzzleCompleted -= OnPuzzleCompleted;
                _puzzleManager.OnPuzzleFailed -= OnPuzzleFailed;
                _puzzleManager.OnPuzzleReset -= OnPuzzleReset;
            }
        }

        private void OnPuzzleAttemptUsed(string puzzleId, int remainingAttempts)
        {
            Debug.Log($"<color=yellow>[PuzzleTestHelper] 이벤트 - 시도 사용: {puzzleId}, 남은 시도: {remainingAttempts}</color>");
        }

        private void OnPuzzleCompleted(string puzzleId)
        {
            Debug.Log($"<color=green>[PuzzleTestHelper] 이벤트 - 퍼즐 완료: {puzzleId}</color>");
        }

        private void OnPuzzleFailed(string puzzleId)
        {
            Debug.Log($"<color=red>[PuzzleTestHelper] 이벤트 - 퍼즐 실패: {puzzleId}</color>");
        }

        private void OnPuzzleReset(string puzzleId)
        {
            Debug.Log($"<color=cyan>[PuzzleTestHelper] 이벤트 - 퍼즐 리셋: {puzzleId}</color>");
        }
    }
}