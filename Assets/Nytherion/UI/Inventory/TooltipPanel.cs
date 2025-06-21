using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;


namespace Nytherion.UI.Inventory
{
    public class TooltipPanel : MonoBehaviour
    {
        public static TooltipPanel Instance { get; private set; }

        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image itemImage;

        private void Awake()
        {
            if (Instance == null) 
                Instance = this;
            else 
                Destroy(gameObject);

            canvasGroup.blocksRaycasts = false;
            
            HideTooltip();
        }

        private RectTransform rectTransform;
        private Canvas canvas;
        private Vector2 screenSize;

        private void Start()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            Canvas.willRenderCanvases += OnCanvasRender;
        }

        private void OnDestroy()
        {
            Canvas.willRenderCanvases -= OnCanvasRender;
        }

        private void OnCanvasRender()
        {
            screenSize = canvas.GetComponent<RectTransform>().sizeDelta;
        }

        private void LateUpdate()
        {
            if (!panel.activeSelf) return;

            Vector2 mousePosition = Input.mousePosition;
            
            Vector2 tooltipSize = rectTransform.sizeDelta;
            
            Vector2 pivot = new Vector2(0, 1);
            
            float rightEdge = mousePosition.x + tooltipSize.x * canvas.scaleFactor;
            float bottomEdge = mousePosition.y - tooltipSize.y * canvas.scaleFactor;
            
            if (rightEdge > Screen.width)
            {
                pivot.x = 1;
            }
            
            if (bottomEdge < 0)
            {
                pivot.y = 0;
            }
            
            if (rectTransform.pivot != pivot)
            {
                rectTransform.pivot = pivot;
                rectTransform.position = mousePosition;
            }
            else
            {
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
