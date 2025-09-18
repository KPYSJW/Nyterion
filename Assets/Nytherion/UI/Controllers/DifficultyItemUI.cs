using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

namespace Nytherion.UI.Controllers
{
    /// <summary>
    /// 퍼즐 난이도 선택 아이템 UI
    /// - 호버 효과 지원
    /// - 클릭 이벤트 처리
    /// - 선택 상태 표시
    /// </summary>
    public class DifficultyItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private Image backgroundImage;         // 배경 이미지
        [SerializeField] private Image difficultyImage;         // 난이도 대표 이미지
        [SerializeField] private TextMeshProUGUI titleText;     // 난이도 제목 (예: "Easy", "Hard")
        [SerializeField] private TextMeshProUGUI levelText;     // 레벨 정보 (예: "Level 1-3")
        [SerializeField] private Button selectButton;           // 선택 버튼

        [Header("Visual Settings")]
        [SerializeField] private Color normalColor = Color.white;           // 기본 색상
        [SerializeField] private Color hoverColor = Color.yellow;           // 호버 색상
        [SerializeField] private Color selectedColor = Color.green;         // 선택된 색상
        [SerializeField] private Vector3 normalScale = Vector3.one;         // 기본 크기
        [SerializeField] private Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1.1f);  // 호버 크기
        [SerializeField] private float animationDuration = 0.2f;            // 애니메이션 지속 시간

        [Header("Difficulty Data")]
        [SerializeField] private int difficultyLevel = 1;                   // 난이도 레벨
        [SerializeField] private string difficultyTitle = "Easy";           // 난이도 제목
        [SerializeField] private string levelRange = "Level 1-3";           // 레벨 범위

        // 이벤트
        public event Action<int> OnDifficultySelected;                      // 난이도 선택 이벤트

        // 상태
        private bool isHovered = false;
        private bool isSelected = false;
        private Coroutine currentAnimation;

        private void Start()
        {
            InitializeUI();
            SetupButton();
        }

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            UpdateVisuals();
            UpdateTexts();
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButton()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectButtonClicked);
            }
        }

        /// <summary>
        /// 난이도 데이터 설정
        /// </summary>
        /// <param name="level">난이도 레벨</param>
        /// <param name="title">난이도 제목</param>
        /// <param name="levelRange">레벨 범위</param>
        /// <param name="image">난이도 대표 이미지</param>
        public void SetDifficultyData(int level, string title, string levelRange, Sprite image = null)
        {
            this.difficultyLevel = level;
            this.difficultyTitle = title;
            this.levelRange = levelRange;

            if (image != null && difficultyImage != null)
            {
                difficultyImage.sprite = image;
            }

            UpdateTexts();
        }

        /// <summary>
        /// 텍스트 업데이트
        /// </summary>
        private void UpdateTexts()
        {
            if (titleText != null)
            {
                titleText.text = difficultyTitle;
            }

            if (levelText != null)
            {
                levelText.text = levelRange;
            }
        }

        /// <summary>
        /// 시각적 요소 업데이트
        /// </summary>
        private void UpdateVisuals()
        {
            Color targetColor = normalColor;
            Vector3 targetScale = normalScale;

            if (isSelected)
            {
                targetColor = selectedColor;
                targetScale = hoverScale; // 선택된 상태에서는 약간 크게
            }
            else if (isHovered)
            {
                targetColor = hoverColor;
                targetScale = hoverScale;
            }

            // 색상 변경
            if (backgroundImage != null)
            {
                backgroundImage.color = targetColor;
            }

            // 스케일 애니메이션
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
            }
            currentAnimation = StartCoroutine(AnimateScale(targetScale));
        }

        /// <summary>
        /// 스케일 애니메이션
        /// </summary>
        private System.Collections.IEnumerator AnimateScale(Vector3 targetScale)
        {
            Vector3 startScale = transform.localScale;
            float elapsedTime = 0f;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / animationDuration;
                t = Mathf.SmoothStep(0f, 1f, t); // 부드러운 애니메이션

                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            currentAnimation = null;
        }

        /// <summary>
        /// 선택 상태 설정
        /// </summary>
        /// <param name="selected">선택 여부</param>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisuals();
        }

        /// <summary>
        /// 마우스 진입 (호버 시작)
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateVisuals();

            // 호버 사운드 효과 추가 가능
            Debug.Log($"[DifficultyItemUI] Hovered: {difficultyTitle}");
        }

        /// <summary>
        /// 마우스 이탈 (호버 종료)
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateVisuals();
        }

        /// <summary>
        /// 마우스 클릭
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            OnSelectButtonClicked();
        }

        /// <summary>
        /// 선택 버튼 클릭 이벤트
        /// </summary>
        private void OnSelectButtonClicked()
        {
            Debug.Log($"[DifficultyItemUI] Selected difficulty: {difficultyTitle} (Level {difficultyLevel})");

            // 선택 이벤트 발생
            OnDifficultySelected?.Invoke(difficultyLevel);

            // 클릭 사운드 효과 추가 가능
        }

        /// <summary>
        /// 컴포넌트 정리
        /// </summary>
        private void OnDestroy()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(OnSelectButtonClicked);
            }

            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
            }
        }

        #region Public Getters
        public int DifficultyLevel => difficultyLevel;
        public string DifficultyTitle => difficultyTitle;
        public bool IsSelected => isSelected;
        public bool IsHovered => isHovered;
        #endregion
    }
}