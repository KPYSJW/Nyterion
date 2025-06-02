using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;

/// <summary>
/// 툴팁 패널을 관리하는 클래스입니다.
/// 아이템에 대한 추가 정보를 표시하는 UI 요소를 제어합니다.
/// </summary>

namespace Nytherion.UI.Inventory
{
    public class TooltipPanel : MonoBehaviour
    {
        public static TooltipPanel Instance;

        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image itemImage;

        private void Awake()
        {
            // 싱글톤 패턴 적용
            if (Instance == null) 
                Instance = this;
            else 
                Destroy(gameObject);

            // 툴팁이 마우스 이벤트를 가로채지 않도록 설정
            canvasGroup.blocksRaycasts = false;
            
            // 시작 시 툴팁 숨기기
            HideTooltip();
        }

        private RectTransform rectTransform;
        private Canvas canvas;
        private Vector2 screenSize;

        private void Start()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            // 화면 해상도 변경을 감지하기 위한 이벤트 등록
            Canvas.willRenderCanvases += OnCanvasRender;
        }

        private void OnDestroy()
        {
            Canvas.willRenderCanvases -= OnCanvasRender;
        }

        private void OnCanvasRender()
        {
            // 화면 크기 업데이트
            screenSize = canvas.GetComponent<RectTransform>().sizeDelta;
        }

        private void LateUpdate()
        {
            if (!panel.activeSelf) return;

            // 마우스 위치 가져오기
            Vector2 mousePosition = Input.mousePosition;
            
            // 툴팁 크기 가져오기
            Vector2 tooltipSize = rectTransform.sizeDelta;
            
            // 피봇 초기값 설정 (좌측 상단)
            Vector2 pivot = new Vector2(0, 1);
            
            // 화면 경계 계산
            float rightEdge = mousePosition.x + tooltipSize.x * canvas.scaleFactor;
            float bottomEdge = mousePosition.y - tooltipSize.y * canvas.scaleFactor;
            
            // 오른쪽으로 넘치는 경우 피봇을 우측으로 변경
            if (rightEdge > Screen.width)
            {
                pivot.x = 1;
            }
            
            // 아래로 넘치는 경우 피봇을 상단으로 변경
            if (bottomEdge < 0)
            {
                pivot.y = 0;
            }
            
            // 피봇이 변경된 경우에만 설정 (불필요한 리빌드 방지)
            if (rectTransform.pivot != pivot)
            {
                rectTransform.pivot = pivot;
                // 피봇 변경 후 위치 재조정
                rectTransform.position = mousePosition;
            }
            else
            {
                // 피봇이 변경되지 않은 경우 위치만 업데이트
                rectTransform.position = mousePosition;
            }
        }

        public void ShowTooltip(ItemData item)
        {
            if (item == null) 
                return;
                
            SetContent(item.itemName, item.description);
            
            if (itemImage != null)
            {
                if (item.icon != null)
                {
                    itemImage.sprite = item.icon;
                    itemImage.preserveAspect = true;
                    itemImage.gameObject.SetActive(true);
                }
                else
                {
                    itemImage.gameObject.SetActive(false);
                }
            }
            
            panel.SetActive(true);
        }

        public void HideTooltip()
        {
            panel.SetActive(false);
            if (itemImage != null)
            {
                itemImage.gameObject.SetActive(false);
            }
        }

        private void SetContent(string name, string desc)
        {
            if (nameText != null)
                nameText.text = name;
                
            if (descriptionText != null)
                descriptionText.text = desc;
        }
    }
}
