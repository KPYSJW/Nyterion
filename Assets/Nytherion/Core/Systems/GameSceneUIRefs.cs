using Nytherion.UI.RelicBoard;
using Nytherion.UI.Inventory;
using Nytherion.UI.Map;
using Nytherion.UI.Shop;
using Nytherion.UI.Skill;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneUIRefs : MonoBehaviour
{
    [Header("Core UI Components")]
    [SerializeField] private RelicGridUI relicGridUI;
    public RelicGridUI RelicGridUI => relicGridUI;
   
    [SerializeField] private RelicTooltip relicTooltip;
    public RelicTooltip RelicTooltip => relicTooltip;


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

    [Header("Gacha Panels")]
    [SerializeField] private GameObject gachaMainPanel;
    public GameObject GachaMainPanel => gachaMainPanel;
    [SerializeField] private GameObject relicResultPanel;
    public GameObject RelicResultPanel => relicResultPanel;
    [SerializeField] private GameObject relicSubPanel;
    public GameObject RelicSubPanel => relicSubPanel;

    [Header("Gacha UI Buttons")]
    [SerializeField] private Button drawOnceBtton;
    public Button DrawOnceButton => drawOnceBtton;

    [SerializeField] private Button drawTenBtton;
    public Button DrawTenButton => drawTenBtton;
    
    [SerializeField] private Button gachaCloseButton;
    public Button GachaCloseButton => gachaCloseButton;
    [SerializeField] private Button relicResultCloseButton;
    public Button RelicResultCloseButton => relicResultCloseButton;

    [Header("Gacha Type Indicator Text")]
    [SerializeField] private TextMeshProUGUI gachaTypeTitleText;
    public TextMeshProUGUI GachaTypeTitleText => gachaTypeTitleText;

    [Header("Gacha Result Panel")]
    [SerializeField] private Transform relicResultSlotParent;
    public Transform RelicResultSlotParent => relicResultSlotParent;
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
    [SerializeField] private ShopSlotUI[] shopSlots;
    public ShopSlotUI[] ShopSlots => shopSlots;
    [SerializeField] private Button shopCloseButton;
    public Button ShopCloseButton => shopCloseButton;
    [SerializeField] private TextMeshProUGUI shopPlayerGoldText;
    public TextMeshProUGUI ShopPlayerGoldText => shopPlayerGoldText;
    [SerializeField] private Transform shopPlayerInventoryParent;
    public Transform ShopPlayerInventoryParent => shopPlayerInventoryParent;
    [SerializeField] private GameObject shopEmptyStateUI;
    public GameObject ShopEmptyStateUI => shopEmptyStateUI;

    [Header("Shop Buy Popup")]
    [SerializeField] private BuyPopupUI shopBuyPopupUI;
    public BuyPopupUI ShopBuyPopupUI => shopBuyPopupUI;

    [Header("Shop Sell Popup")]
    [SerializeField] private SellPopupUI shopSellPopupUI;
    public SellPopupUI ShopSellPopupUI => shopSellPopupUI;

    [Header("Shop Reroll UI")]
    [SerializeField] private Button shopRerollButton;
    public Button ShopRerollButton => shopRerollButton;
    [SerializeField] private TextMeshProUGUI shopRerollCostText;
    public TextMeshProUGUI ShopRerollCostText => shopRerollCostText;
    [SerializeField] private Button shopAdvancedRerollButton;
    public Button ShopAdvancedRerollButton => shopAdvancedRerollButton;
    [SerializeField] private TextMeshProUGUI shopAdvancedRerollCostText;
    public TextMeshProUGUI ShopAdvancedRerollCostText => shopAdvancedRerollCostText;

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

    [Header("Relic UI")]
    [SerializeField] private CanvasGroup relicCanvasGroup;
    public CanvasGroup RelicCanvasGroup => relicCanvasGroup;
    [SerializeField] private RectTransform relicSetEffectStatusPanel;
    public RectTransform RelicSetEffectStatusPanel => relicSetEffectStatusPanel;
    [SerializeField] private RectTransform relicTranscendenceEffectStatusPanel;
    public RectTransform RelicTranscendenceEffectStatusPanel => relicTranscendenceEffectStatusPanel;
    [SerializeField] private GameObject relicEffectStatusEntryPrefab;
    public GameObject RelicEffectStatusEntryPrefab => relicEffectStatusEntryPrefab;
    public GameObject RelicSetEffectStatusEntryPrefab => relicEffectStatusEntryPrefab;
    [SerializeField] private GameObject relicTranscendenceEffectStatusEntryPrefab;
    public GameObject RelicTranscendenceEffectStatusEntryPrefab => relicTranscendenceEffectStatusEntryPrefab;

    [Header("Dungeon UI")]
    [SerializeField] private WorldmapController worldmapController;
    public WorldmapController WorldmapController => worldmapController;
    [SerializeField] private MinimapTileGenerator minimapTileGenerator;
    public MinimapTileGenerator MinimapTileGenerator => minimapTileGenerator;

    [Header("Currency Display")]
    [SerializeField] private CurrencyDisplay[] currencyDisplays;
    public CurrencyDisplay[] CurrencyDisplays => currencyDisplays;

    [Header("Progression UI")]
    public GameObject ProgressionMainPanel; 
    public TMP_Text ProgressionTitleText;   
    public Transform ProgressionSlotParent;

    [Header("Skill UI")]
    public GameObject SkillMainPanel;
    public Transform storageContent;
    public SkillStorageArea storageDropArea;
    public SkillSlotUI[] equipSlots;
    private void Awake()
    {
        if (shopPlayerInventoryParent == null && inventorySlotParent != null)
        {
            shopPlayerInventoryParent = inventorySlotParent;
        }
    }

}
