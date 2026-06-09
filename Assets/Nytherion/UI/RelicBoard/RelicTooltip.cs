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
        
        // Pivot을 (0, 1) [좌측 상단]으로 고정하여 계산을 단순화합니다.
        rectTransform.pivot = new Vector2(0, 1);
        
        float width = rectTransform.rect.width * canvas.scaleFactor;
        float height = rectTransform.rect.height * canvas.scaleFactor;
        
        float margin = 10f;
        float cursorOffset = 15f;
        
        // 기본 위치: 마우스 우측 하단
        float targetX = mousePosition.x + cursorOffset;
        float targetY = mousePosition.y - cursorOffset;
        
        // 화면 오른쪽 경계를 넘어가는 경우 마우스 좌측으로 배치
        if (targetX + width > Screen.width - margin)
        {
            targetX = mousePosition.x - width - cursorOffset;
        }
        // 마우스 좌측으로 배치했을 때도 화면 왼쪽 경계를 넘어가는 경우 화면 좌측 경계에 맞춤
        if (targetX < margin)
        {
            targetX = margin;
        }
        
        // 화면 아래쪽 경계를 넘어가는 경우 마우스 상단으로 배치
        if (targetY - height < margin)
        {
            targetY = mousePosition.y + height + cursorOffset;
        }
        // 마우스 상단으로 배치했을 때도 화면 위쪽 경계를 넘어가는 경우 화면 위쪽 경계에 맞춤
        if (targetY > Screen.height - margin)
        {
            targetY = Screen.height - margin;
        }
        
        rectTransform.position = new Vector2(targetX, targetY);
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