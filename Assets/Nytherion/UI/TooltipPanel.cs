using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Inventory
{
    public class TooltipPanel : MonoBehaviour
    {
        public static TooltipPanel Instance;

        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            canvasGroup.blocksRaycasts = false; // 마우스 방해 방지
            HideTooltip();
        }

        private void LateUpdate()
        {
            if (panel.activeSelf)
            {
                transform.position = Input.mousePosition;
            }
        }

        public void ShowTooltip(ItemData item)
        {
            if (item == null) return;
            SetContent(item.itemName, item.description);
            panel.SetActive(true);
        }

        public void HideTooltip()
        {
            panel.SetActive(false);
        }

        private void SetContent(string name, string desc)
        {
            nameText.text = name;
            descriptionText.text = desc;
        }
    }
}
