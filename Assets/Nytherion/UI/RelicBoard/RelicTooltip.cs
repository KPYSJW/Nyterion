using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.GamePlay.Relics;

namespace Nytherion.UI.RelicBoard
{
    public class RelicTooltip : MonoBehaviour
{
    public static RelicTooltip Instance { get; private set; }

    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Influence Grid")]
    [SerializeField] private Image[] influenceCells = new Image[9];
    [SerializeField] private Color centerColor = Color.yellow;
    [SerializeField] private Color levelUpColor = Color.green;
    [SerializeField] private Color levelDownColor = Color.red;
    [SerializeField] private Color neutralColor = new Color(50/255f, 50/255f, 50/255f, 200/255f);

    private RectTransform rectTransform;
    private Canvas canvas;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rectTransform = tooltipPanel.GetComponent<RectTransform>();
        Hide();
    }

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>().rootCanvas;
    }

    private void LateUpdate()
    {
        if (!tooltipPanel.activeSelf || canvas == null) return;

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

    public void Show(RelicBlock block)
    {
        if (block == null) return;

        string name = !string.IsNullOrEmpty(block.SourceData.koreanName) ? block.SourceData.koreanName : block.SourceData.relicName;
        nameText.text = $"{name} <size=80%><color=#AAAAAA>Lv. {block.SourceData.level}</color></size>";
        descriptionText.text = block.SourceData.Description;

        UpdateInfluenceGrid(block);

        tooltipPanel.SetActive(true);
    }

    public void Hide()
    {
        tooltipPanel.SetActive(false);
    }

    private void UpdateInfluenceGrid(RelicBlock block)
    {
        foreach (var cell in influenceCells)
        {
            cell.color = neutralColor;
        }

        influenceCells[4].color = centerColor;

        foreach (var zone in block.GetRotatedInfluenceZones())
        {
            int rowIndex = zone.offset.y * -1 + 1; 
            int colIndex = 1 + zone.offset.x; 
            int index = (rowIndex * 3) + colIndex;

            if (index >= 0 && index < 9)
            {
                influenceCells[index].color = zone.type == InfluenceType.LevelUp ? levelUpColor : levelDownColor;
            }
        }
    }
}
}