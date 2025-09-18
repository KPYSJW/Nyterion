using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.UI.Components;
using Nytherion.Core.Systems;
using System.Collections;
using System.Collections.Generic;

namespace Nytherion.UI.Controllers
{
    /// <summary>
    /// 퍼즐 난이도 선택 UI 컨트롤러
    /// - 가로 스크롤 가능한 난이도 선택 인터페이스
    /// - 호버 효과와 선택 상태 관리
    /// - 기존 난이도 낮춤 기능과 새로운 스크롤 UI 통합
    /// </summary>
    public class DifficultySelectionUIController : UIPanelBase
    {
        // UI 참조는 GameSceneUIRefs를 통해 주입받음
        private GameObject difficultySelectionPanel;
        private ScrollRect scrollRect;
        private Transform contentParent;
        private Button closeButton;
        private GameObject difficultyItemPrefab;

        [Header("Scroll-based Difficulty Selection")]
        [SerializeField] private List<DifficultyLevelData> difficultyLevels = new List<DifficultyLevelData>();
        [SerializeField] private float scrollSensitivity = 1.0f;
        [SerializeField] private bool enableKeyboardNavigation = true;

        [Header("Difficulty Down Offer UI")]
        [SerializeField] private GameObject difficultyDownPanel;
        [SerializeField] private TextMeshProUGUI offerTitleText;
        [SerializeField] private TextMeshProUGUI offerMessageText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        // Dependencies
        private PuzzleManager puzzleManager;
        private PuzzleUIController puzzleUIController;
        private GameSceneUIRefs uiRefs;

        // 스크롤 UI 관련
        private List<DifficultyItemUI> spawnedItems = new List<DifficultyItemUI>();
        private int currentSelectedIndex = -1;

        [Inject]
        public void Construct(PuzzleManager puzzleManager, PuzzleUIController puzzleUIController, GameSceneUIRefs uiRefs)
        {
            this.puzzleManager = puzzleManager;
            this.puzzleUIController = puzzleUIController;
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
                Debug.LogError("[DifficultySelectionUIController] GameSceneUIRefs not found!");
                return;
            }

            difficultySelectionPanel = uiRefs.DifficultySelectionPanel;
            scrollRect = uiRefs.DifficultyScrollRect;
            contentParent = uiRefs.DifficultyContentParent;
            closeButton = uiRefs.DifficultyCloseButton;
            difficultyItemPrefab = uiRefs.DifficultyItemPrefab;
        }

        private void Start()
        {
            SetupUI();
            SubscribeToEvents();
            InitializeDefaultDifficulties();
            PopulateDifficultyItems();
        }

        private void Update()
        {
            HandleKeyboardInput();
        }

        private void SetupUI()
        {
            // 닫기 버튼
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCancelButtonClicked);

            // 난이도 낮춤 제안 버튼들
            if (acceptButton != null)
                acceptButton.onClick.AddListener(OnAcceptDifficultyDown);

            if (declineButton != null)
                declineButton.onClick.AddListener(OnDeclineDifficultyDown);

            // 초기 상태
            if (difficultySelectionPanel != null)
                difficultySelectionPanel.SetActive(false);

            if (difficultyDownPanel != null)
                difficultyDownPanel.SetActive(false);

            // 스크롤 설정
            if (scrollRect != null)
            {
                scrollRect.horizontal = true;
                scrollRect.vertical = false;
                scrollRect.scrollSensitivity = scrollSensitivity;
            }
        }

        /// <summary>
        /// 기본 난이도 데이터 초기화
        /// </summary>
        private void InitializeDefaultDifficulties()
        {
            if (difficultyLevels.Count == 0)
            {
                difficultyLevels.Add(new DifficultyLevelData
                {
                    level = 1,
                    title = "Beginner",
                    levelRange = "3x3 Grid",
                    description = "Perfect for learning the basics",
                    attempts = 5,
                    image = null
                });

                difficultyLevels.Add(new DifficultyLevelData
                {
                    level = 2,
                    title = "Easy",
                    levelRange = "4x4 Grid",
                    description = "Simple puzzles with 2-3 colors",
                    attempts = 4,
                    image = null
                });

                difficultyLevels.Add(new DifficultyLevelData
                {
                    level = 3,
                    title = "Normal",
                    levelRange = "5x5 Grid",
                    description = "Moderate challenge with 3-4 colors",
                    attempts = 3,
                    image = null
                });

                difficultyLevels.Add(new DifficultyLevelData
                {
                    level = 4,
                    title = "Hard",
                    levelRange = "6x6 Grid",
                    description = "Complex puzzles requiring strategy",
                    attempts = 3,
                    image = null
                });

                difficultyLevels.Add(new DifficultyLevelData
                {
                    level = 5,
                    title = "Expert",
                    levelRange = "7x7 Grid",
                    description = "Master-level challenges",
                    attempts = 2,
                    image = null
                });
            }
        }

        private void SubscribeToEvents()
        {
            if (puzzleManager != null)
            {
                puzzleManager.OnDifficultyDownOffered += OnDifficultyDownOffered;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (puzzleManager != null)
            {
                puzzleManager.OnDifficultyDownOffered -= OnDifficultyDownOffered;
            }
        }

        #region New Scroll-based Difficulty Selection

        /// <summary>
        /// 키보드 입력 처리 (좌우 화살표 키)
        /// </summary>
        private void HandleKeyboardInput()
        {
            if (!enableKeyboardNavigation || !difficultySelectionPanel.activeInHierarchy)
                return;

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                NavigateDifficulty(-1);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                NavigateDifficulty(1);
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                SelectCurrentDifficulty();
            }
        }

        /// <summary>
        /// 키보드로 난이도 탐색
        /// </summary>
        private void NavigateDifficulty(int direction)
        {
            if (spawnedItems.Count == 0) return;

            int newIndex = currentSelectedIndex + direction;
            newIndex = Mathf.Clamp(newIndex, 0, spawnedItems.Count - 1);

            if (newIndex != currentSelectedIndex)
            {
                SetKeyboardSelection(newIndex);
                ScrollToItem(newIndex);
            }
        }

        /// <summary>
        /// 키보드 선택 상태 설정
        /// </summary>
        private void SetKeyboardSelection(int index)
        {
            currentSelectedIndex = index;
        }

        /// <summary>
        /// 현재 선택된 난이도로 게임 시작
        /// </summary>
        private void SelectCurrentDifficulty()
        {
            if (currentSelectedIndex >= 0 && currentSelectedIndex < spawnedItems.Count)
            {
                var selectedItem = spawnedItems[currentSelectedIndex];
                OnScrollDifficultySelected(selectedItem.DifficultyLevel);
            }
        }

        /// <summary>
        /// 선택된 아이템으로 스크롤
        /// </summary>
        private void ScrollToItem(int index)
        {
            if (scrollRect == null || spawnedItems.Count == 0 || index < 0 || index >= spawnedItems.Count)
                return;

            var targetItem = spawnedItems[index];
            var contentRectTransform = scrollRect.content;
            var viewportRectTransform = scrollRect.viewport;

            // 아이템 위치 계산
            Vector3 itemPosition = contentRectTransform.InverseTransformPoint(targetItem.transform.position);
            Vector3 viewportCenter = contentRectTransform.InverseTransformPoint(viewportRectTransform.position);

            // 수평 스크롤 위치 계산
            float targetX = itemPosition.x - viewportCenter.x;
            Vector2 targetPosition = new Vector2(targetX, contentRectTransform.anchoredPosition.y);

            // 부드러운 스크롤 애니메이션
            StartCoroutine(SmoothScrollTo(targetPosition));
        }

        /// <summary>
        /// 부드러운 스크롤 애니메이션
        /// </summary>
        private IEnumerator SmoothScrollTo(Vector2 targetPosition)
        {
            Vector2 startPosition = scrollRect.content.anchoredPosition;
            float duration = 0.3f;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                t = Mathf.SmoothStep(0f, 1f, t);

                scrollRect.content.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            scrollRect.content.anchoredPosition = targetPosition;
        }

        /// <summary>
        /// 난이도 아이템들 생성
        /// </summary>
        private void PopulateDifficultyItems()
        {
            if (difficultyItemPrefab == null || contentParent == null)
            {
                Debug.LogError("[DifficultySelectionUIController] Missing prefab or content parent!");
                return;
            }

            // 기존 아이템들 제거
            ClearExistingItems();

            // 새 아이템들 생성
            for (int i = 0; i < difficultyLevels.Count; i++)
            {
                CreateDifficultyItem(difficultyLevels[i], i);
            }

            Debug.Log($"[DifficultySelectionUIController] Created {spawnedItems.Count} difficulty items");
        }

        /// <summary>
        /// 기존 아이템들 제거
        /// </summary>
        private void ClearExistingItems()
        {
            foreach (var item in spawnedItems)
            {
                if (item != null)
                {
                    item.OnDifficultySelected -= OnScrollDifficultySelected;
                    Destroy(item.gameObject);
                }
            }
            spawnedItems.Clear();
        }

        /// <summary>
        /// 난이도 아이템 생성
        /// </summary>
        private void CreateDifficultyItem(DifficultyLevelData data, int index)
        {
            GameObject itemObj = Instantiate(difficultyItemPrefab, contentParent);
            DifficultyItemUI itemUI = itemObj.GetComponent<DifficultyItemUI>();

            if (itemUI != null)
            {
                itemUI.SetDifficultyData(data.level, data.title, data.levelRange, data.image);
                itemUI.OnDifficultySelected += OnScrollDifficultySelected;
                spawnedItems.Add(itemUI);

                Debug.Log($"[DifficultySelectionUIController] Created difficulty item: {data.title} (Level {data.level})");
            }
            else
            {
                Debug.LogError("[DifficultySelectionUIController] DifficultyItemUI component not found on prefab!");
                Destroy(itemObj);
            }
        }

        /// <summary>
        /// 스크롤 UI에서 난이도 선택 이벤트 처리
        /// </summary>
        private void OnScrollDifficultySelected(int difficultyLevel)
        {
            Debug.Log($"[DifficultySelectionUIController] Scroll difficulty {difficultyLevel} selected");

            // 선택된 아이템 표시 업데이트
            UpdateSelectedItem(difficultyLevel);

            // 퍼즐 매니저에서 해당 난이도 레벨 로드
            if (puzzleManager != null)
            {
                puzzleManager.LoadLevel(difficultyLevel - 1); // 0-based index
            }

            // 퍼즐 UI 표시
            if (puzzleUIController != null)
            {
                puzzleUIController.ShowPuzzle();
            }

            // 난이도 선택 UI 숨김
            HideDifficultySelection();
        }

        /// <summary>
        /// 선택된 아이템 상태 업데이트
        /// </summary>
        private void UpdateSelectedItem(int selectedLevel)
        {
            foreach (var item in spawnedItems)
            {
                if (item != null)
                {
                    item.SetSelected(item.DifficultyLevel == selectedLevel);
                }
            }
        }

        #endregion

        #region Legacy Difficulty Selection UI

        public void ShowDifficultySelection()
        {
            if (difficultySelectionPanel != null)
            {
                difficultySelectionPanel.SetActive(true);
                currentSelectedIndex = 0; // 첫 번째 항목으로 초기화
            }

            Debug.Log("[DifficultySelectionUI] Showing difficulty selection");
        }

        public void HideDifficultySelection()
        {
            if (difficultySelectionPanel != null)
                difficultySelectionPanel.SetActive(false);
        }

        private void OnDifficultySelected(DifficultyLevel difficulty)
        {
            Debug.Log($"[DifficultySelectionUI] Selected difficulty: {difficulty}");

            // 선택된 난이도에 따라 적절한 레벨 로드
            int levelIndex = GetLevelIndexForDifficulty(difficulty);

            if (puzzleManager != null)
            {
                puzzleManager.LoadLevel(levelIndex);
            }

            HideDifficultySelection();

            // 퍼즐 UI 표시
            if (puzzleUIController != null)
            {
                puzzleUIController.ShowPuzzle();
            }
        }

        private int GetLevelIndexForDifficulty(DifficultyLevel difficulty)
        {
            // 난이도에 따른 레벨 인덱스 매핑 (실제 구현에서는 설정 가능하도록 개선)
            switch (difficulty)
            {
                case DifficultyLevel.Easy:
                    return 0; // 첫 번째 레벨 (쉬운 설정)
                case DifficultyLevel.Normal:
                    return 1; // 두 번째 레벨 (보통 설정)
                case DifficultyLevel.Hard:
                    return 2; // 세 번째 레벨 (어려운 설정)
                default:
                    return 0;
            }
        }

        private void OnCancelButtonClicked()
        {
            HideDifficultySelection();
            Debug.Log("[DifficultySelectionUI] Difficulty selection cancelled");
        }

        #endregion

        #region Difficulty Down Offer UI

        private void OnDifficultyDownOffered()
        {
            ShowDifficultyDownOffer();
        }

        private void ShowDifficultyDownOffer()
        {
            if (difficultyDownPanel != null)
            {
                difficultyDownPanel.SetActive(true);

                if (offerTitleText != null)
                    offerTitleText.text = "난이도 낮춤 제안";

                if (offerMessageText != null)
                    offerMessageText.text = $"연속으로 {puzzleManager.ConsecutiveFailures}회 실패했습니다.\n난이도를 낮춰 시도 횟수를 1회 추가하시겠습니까?";
            }

            Debug.Log("[DifficultySelectionUI] Showing difficulty down offer");
        }

        private void HideDifficultyDownOffer()
        {
            if (difficultyDownPanel != null)
                difficultyDownPanel.SetActive(false);
        }

        private void OnAcceptDifficultyDown()
        {
            if (puzzleManager != null)
            {
                puzzleManager.AcceptDifficultyDown();
            }

            HideDifficultyDownOffer();
            Debug.Log("[DifficultySelectionUI] Difficulty down accepted");
        }

        private void OnDeclineDifficultyDown()
        {
            if (puzzleManager != null)
            {
                puzzleManager.DeclineDifficultyDown();
            }

            HideDifficultyDownOffer();
            Debug.Log("[DifficultySelectionUI] Difficulty down declined");
        }

        #endregion

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            // 스크롤 아이템 이벤트 정리
            foreach (var item in spawnedItems)
            {
                if (item != null)
                {
                    item.OnDifficultySelected -= OnScrollDifficultySelected;
                }
            }

            // 이벤트 리스너 정리
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
            if (acceptButton != null)
                acceptButton.onClick.RemoveAllListeners();
            if (declineButton != null)
                declineButton.onClick.RemoveAllListeners();
        }

        #region Public Methods
        /// <summary>
        /// 난이도 데이터 추가
        /// </summary>
        public void AddDifficultyLevel(DifficultyLevelData difficultyData)
        {
            difficultyLevels.Add(difficultyData);
            PopulateDifficultyItems();
        }

        /// <summary>
        /// 특정 난이도로 스크롤
        /// </summary>
        public void ScrollToDifficulty(int difficultyLevel)
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i].DifficultyLevel == difficultyLevel)
                {
                    ScrollToItem(i);
                    break;
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// 난이도 레벨 데이터
    /// </summary>
    [System.Serializable]
    public class DifficultyLevelData
    {
        [Header("Basic Info")]
        public int level;                   // 난이도 레벨 (1부터 시작)
        public string title;                // 난이도 제목 (예: "Easy", "Hard")
        public string levelRange;           // 레벨 범위 또는 그리드 크기 (예: "3x3 Grid")
        public string description;          // 난이도 설명

        [Header("Game Settings")]
        public int attempts = 3;            // 시도 횟수

        [Header("Visual")]
        public Sprite image;                // 난이도 대표 이미지
    }

    // 기존 난이도 레벨 열거형 (호환성을 위해 유지)
    public enum DifficultyLevel
    {
        Easy,
        Normal,
        Hard
    }
}