using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.GamePlay.Engravings;

public class EngravingTooltip : MonoBehaviour
{
    public static EngravingTooltip Instance { get; private set; }

    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Influence Grid")]
    [SerializeField] private Image[] influenceCells = new Image[9];
    [SerializeField] private Color centerColor = Color.yellow;
    [SerializeField] private Color levelUpColor = Color.green;
    [SerializeField] private Color levelDownColor = Color.red;
    [SerializeField] private Color neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

    private RectTransform rectTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rectTransform = tooltipPanel.GetComponent<RectTransform>();
        Hide();
    }

    private void LateUpdate()
    {
        if (tooltipPanel.activeSelf)
        {
            rectTransform.position = Input.mousePosition;

            Vector2 pivot = rectTransform.pivot;
            Vector2 panelSize = rectTransform.sizeDelta;

            Vector2 screenPos = rectTransform.anchoredPosition;

            if (screenPos.x + (panelSize.x * (1 - pivot.x)) > Screen.width)
            {
                screenPos.x = Screen.width - (panelSize.x * (1 - pivot.x));
            }
            if (screenPos.x - (panelSize.x * pivot.x) < 0)
            {
                screenPos.x = (panelSize.x * pivot.x);
            }
            if (screenPos.y - (panelSize.y * pivot.y) < 0)
            {
                screenPos.y = (panelSize.y * pivot.y);
            }

            rectTransform.anchoredPosition = screenPos;
        }
    }

    public void Show(EngravingBlock block)
    {
        if (block == null) return;

        nameText.text = block.SourceData.engravingName;
        levelText.text = $"Lv. {block.SourceData.level}";
        descriptionText.text = block.SourceData.description;

        UpdateInfluenceGrid(block);

        tooltipPanel.SetActive(true);
    }

    public void Hide()
    {
        tooltipPanel.SetActive(false);
    }

    private void UpdateInfluenceGrid(EngravingBlock block)
    {
        foreach (var cell in influenceCells)
        {
            cell.color = neutralColor;
        }

        influenceCells[4].color = centerColor;

        foreach (var zone in block.GetRotatedInfluenceZones())
        {
            int index = (1 - zone.offset.y) * 3 + (1 + zone.offset.x);
            if (index >= 0 && index < 9)
            {
                influenceCells[index].color = zone.type == InfluenceType.LevelUp ? levelUpColor : levelDownColor;
            }
        }
    }
}