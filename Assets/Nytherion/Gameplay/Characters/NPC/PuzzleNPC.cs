using UnityEngine;
using VContainer;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.UI.Controllers;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class PuzzleNPC : MonoBehaviour, IInteractable
    {
        [Header("Puzzle Configuration")]
        [SerializeField] private string interactionPrompt = "퍼즐에 도전하기";

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color availableColor = Color.white;
        [SerializeField] private Color completedColor = Color.green;
        [SerializeField] private Color failedColor = Color.red;

        // 의존성 주입
        private PuzzleManager _puzzleManager;
        private PuzzleUIController _puzzleUIController;
        private GameSceneUIManager _gameSceneUIManager;

        public InteractableType Type => InteractableType.Puzzle;
        public InteractableType InteractableType => InteractableType.Puzzle;
        public string InteractionPrompt => GetCurrentInteractionPrompt();

        [Inject]
        public void Construct(PuzzleManager puzzleManager, PuzzleUIController puzzleUIController, GameSceneUIManager gameSceneUIManager)
        {
            _puzzleManager = puzzleManager;
            _puzzleUIController = puzzleUIController;
            _gameSceneUIManager = gameSceneUIManager;
        }

        private void Start()
        {
            // 퍼즐 매니저 이벤트 구독
            if (_puzzleManager != null)
            {
                _puzzleManager.OnPuzzleCompleted += OnPuzzleCompleted;
                _puzzleManager.OnPuzzleFailed += OnPuzzleFailed;
            }

            // 초기 비주얼 업데이트
            UpdateVisual();
        }

        public void Interact()
        {
            // 퍼즐 시작
            Debug.Log("[PuzzleNPC] 퍼즐과 상호작용");
            StartPuzzle();
        }

        private void StartPuzzle()
        {
            if (_puzzleUIController != null)
            {
                _puzzleUIController.ShowPuzzle();
                Debug.Log($"<color=cyan>[PuzzleNPC] 퍼즐 UI 표시</color>");
            }
            else
            {
                Debug.LogError("[PuzzleNPC] PuzzleUIController를 찾을 수 없습니다!");
            }
        }

        private string GetCurrentInteractionPrompt()
        {
            return interactionPrompt;
        }

        private void UpdateVisual()
        {
            if (spriteRenderer == null) return;
            spriteRenderer.color = availableColor;
        }

        // 퍼즐 매니저 이벤트 핸들러
        private void OnPuzzleCompleted()
        {
            UpdateVisual();
            Debug.Log($"<color=green>[PuzzleNPC] 퍼즐 완료 이벤트 수신</color>");
        }

        private void OnPuzzleFailed()
        {
            UpdateVisual();
            Debug.Log($"<color=red>[PuzzleNPC] 퍼즐 실패 이벤트 수신</color>");
        }

        // 컴포넌트 정리
        private void OnDestroy()
        {
            if (_puzzleManager != null)
            {
                _puzzleManager.OnPuzzleCompleted -= OnPuzzleCompleted;
                _puzzleManager.OnPuzzleFailed -= OnPuzzleFailed;
            }
        }

        // 에디터용 유틸리티
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(interactionPrompt))
            {
                interactionPrompt = "퍼즐 도전";
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 상호작용 범위 표시
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1.5f);

            // 퍼즐 아이콘
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.3f);
        }
#endif
    }
}