using Nytherion.UI.Controllers;
using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Inventory;
using Nytherion.UI.Map;
using Nytherion.UI.Presenters;
using Nytherion.UI.Shop;
using Nytherion.GamePlay.Puzzle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneUIRefs : MonoBehaviour
{
    [Header("Core UI Components")]
    [SerializeField] private EngravingGridUI engravingGridUI;
    public EngravingGridUI EngravingGridUI => engravingGridUI;
   
    [SerializeField] private EngravingTooltip engravingTooltip;
    public EngravingTooltip EngravingTooltip => engravingTooltip;


    [Header("Quick Slot References")]
    [SerializeField] private QuickSlotUI[] quickSlots;
    public QuickSlotUI[] QuickSlots => quickSlots;

    [Header("Inventory UI References")]
    [SerializeField] private Transform inventorySlotParent;
    public Transform InventorySlotParent => inventorySlotParent;
    [SerializeField] private CanvasGroup inventoryCanvasGroup;
    public CanvasGroup InventoryCanvasGroup => inventoryCanvasGroup;
    [SerializeField] private GameObject inventorySlotPrefab;
    public GameObject InventorySlotPrefab => inventorySlotPrefab;
    [SerializeField] private GameObject equipmentPanel;
    public GameObject EquipmentPanel => equipmentPanel;
    [SerializeField] private GameObject statsPanel;
    public GameObject StatsPanel => statsPanel;
    [SerializeField] private Button inventoryCloseButton;
    public Button InventoryCloseButton => inventoryCloseButton;
    

    [Header("Gacha UI References")]
    [SerializeField] private CanvasGroup gachaCanvasGroup;
    public CanvasGroup GachaCanvasGroup => gachaCanvasGroup;
    [SerializeField] private GameObject gachaMainPanel;
    public GameObject GachaMainPanel => gachaMainPanel;
    [SerializeField] private GameObject gachaResultPanel;
    public GameObject GachaResultPanel => gachaResultPanel;
    [SerializeField] private TextMeshProUGUI tokenCountText;
    public TextMeshProUGUI TokenCountText => tokenCountText;

    [Header("Gacha UI Buttons")]
    [SerializeField] private Button drawWeaponOnceButton;
    public Button DrawWeaponOnceButton => drawWeaponOnceButton;
    [SerializeField] private Button drawWeaponTenTimesButton;
    public Button DrawWeaponTenTimesButton => drawWeaponTenTimesButton;
    [SerializeField] private Button drawEngravingOnceButton;
    public Button DrawEngravingOnceButton => drawEngravingOnceButton;
    [SerializeField] private Button drawEngravingTenTimesButton;
    public Button DrawEngravingTenTimesButton => drawEngravingTenTimesButton;
    [SerializeField] private Button gachaCloseButton;
    public Button GachaCloseButton => gachaCloseButton;
    [SerializeField] private Button resultCloseButton;
    public Button ResultCloseButton => resultCloseButton;

    [Header("Gacha Result Panel")]
    [SerializeField] private Transform resultSlotParent;
    public Transform ResultSlotParent => resultSlotParent;
    [SerializeField] private GameObject resultSlotPrefab;
    public GameObject ResultSlotPrefab => resultSlotPrefab;

    [Header("Settings UI")]
    [SerializeField] private Slider masterSlider;
    public Slider MasterSlider => masterSlider;
    [SerializeField] private Slider bgmSlider;
    public Slider BgmSlider => bgmSlider;
    [SerializeField] private Slider sfxSlider;
    public Slider SfxSlider => sfxSlider;
    [SerializeField] private Toggle fullscreenToggle;
    public Toggle FullscreenToggle => fullscreenToggle;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown ResolutionDropdown => resolutionDropdown;

    [Header("Shop UI")]
    [SerializeField] private GameObject shopUIPrefab;
    public GameObject ShopUIPrefab => shopUIPrefab;
    [SerializeField] private CanvasGroup shopCanvasGroup;
    public CanvasGroup ShopCanvasGroup => shopCanvasGroup;
    [SerializeField] private Transform shopSlotParent;
    public Transform ShopSlotParent => shopSlotParent;
    [SerializeField] private GameObject shopSlotPrefab;
    public GameObject ShopSlotPrefab => shopSlotPrefab;
    [SerializeField] private Button shopCloseButton;
    public Button ShopCloseButton => shopCloseButton;
    [SerializeField] private TextMeshProUGUI shopPlayerGoldText;
    public TextMeshProUGUI ShopPlayerGoldText => shopPlayerGoldText;
    [SerializeField] private Transform shopPlayerInventoryParent;
    public Transform ShopPlayerInventoryParent => shopPlayerInventoryParent;
    [SerializeField] private SellSlotUI sellSlotUI;
    public SellSlotUI SellSlotUI => sellSlotUI;


    [Header("Menu UI")]
    [SerializeField] private CanvasGroup menuCanvasGroup;
    public CanvasGroup MenuCanvasGroup => menuCanvasGroup;
    [SerializeField] private GameObject menuMainPanel;
    public GameObject MenuMainPanel => menuMainPanel;
    [SerializeField] private Button menuResumeButton;
    public Button MenuResumeButton => menuResumeButton;
    [SerializeField] private Button menuSettingsButton;
    public Button MenuSettingsButton => menuSettingsButton;
    [SerializeField] private GameObject menuSettingsPanel;
    public GameObject MenuSettingsPanel => menuSettingsPanel;
    [SerializeField] private Button menuControlButton;
    public Button MenuControlButton => menuControlButton;
    [SerializeField] private GameObject menuControlsPanel;
    public GameObject MenuControlsPanel => menuControlsPanel;
    [SerializeField] private Button menuMainMenuButton;
    public Button MenuMainMenuButton => menuMainMenuButton;

    [Header("Engraving UI")]
    [SerializeField] private CanvasGroup engravingCanvasGroup;
    public CanvasGroup EngravingCanvasGroup => engravingCanvasGroup;

    [Header("Dungeon UI")]
    [SerializeField] private WorldmapController worldmapController;
    public WorldmapController WorldmapController => worldmapController;
    [SerializeField] private MinimapTileGenerator minimapTileGenerator;
    public MinimapTileGenerator MinimapTileGenerator => minimapTileGenerator;

    [Header("Currency Display")]
    [SerializeField] private CurrencyDisplay[] currencyDisplays;
    public CurrencyDisplay[] CurrencyDisplays => currencyDisplays;

    [Header("Puzzle UI References")]
    [SerializeField] private GameObject puzzlePanel;
    public GameObject PuzzlePanel => puzzlePanel;
    [SerializeField] private PuzzleGridView puzzleGridView;
    public PuzzleGridView PuzzleGridView => puzzleGridView;
    [SerializeField] private Button puzzleStartButton;
    public Button PuzzleStartButton => puzzleStartButton;
    [SerializeField] private Button puzzleResetButton;
    public Button PuzzleResetButton => puzzleResetButton;
    [SerializeField] private Button puzzleExitButton;
    public Button PuzzleExitButton => puzzleExitButton;
    [SerializeField] private TextMeshProUGUI puzzleAttemptsText;
    public TextMeshProUGUI PuzzleAttemptsText => puzzleAttemptsText;
    [SerializeField] private TextMeshProUGUI puzzleLevelText;
    public TextMeshProUGUI PuzzleLevelText => puzzleLevelText;
    [SerializeField] private TextMeshProUGUI puzzleStatusText;
    public TextMeshProUGUI PuzzleStatusText => puzzleStatusText;

    [Header("Difficulty Selection UI References")]
    [SerializeField] private GameObject difficultySelectionPanel;
    public GameObject DifficultySelectionPanel => difficultySelectionPanel;
    [SerializeField] private ScrollRect difficultyScrollRect;
    public ScrollRect DifficultyScrollRect => difficultyScrollRect;
    [SerializeField] private Transform difficultyContentParent;
    public Transform DifficultyContentParent => difficultyContentParent;
    [SerializeField] private Button difficultyCloseButton;
    public Button DifficultyCloseButton => difficultyCloseButton;
    [SerializeField] private GameObject difficultyItemPrefab;
    public GameObject DifficultyItemPrefab => difficultyItemPrefab;

    private void Awake()
    {
        if (shopPlayerInventoryParent == null && inventorySlotParent != null)
        {
            shopPlayerInventoryParent = inventorySlotParent;
        }
    }

}