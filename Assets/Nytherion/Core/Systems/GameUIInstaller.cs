using UnityEngine;
using Zenject;
using Nytherion.UI.Controllers;
using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Inventory;
using UnityEngine.UI;
using TMPro;

namespace Nytherion.Core.Systems
{
    public class GameUIInstaller : MonoInstaller
    {
        public void SetContainer(DiContainer container)
        {
            this.Container = container;
        }
        [Header("Core UI Components")]
        [SerializeField] private EngravingGridUI engravingGridUI;
        [SerializeField] private EngravingTooltip engravingTooltip;

        [Header("Inventory UI References")]
        [SerializeField] private Transform inventorySlotParent;
        
        [SerializeField] private CanvasGroup inventoryCanvasGroup;
        [SerializeField] private GameObject equipmentPanel;
        [SerializeField] private GameObject statsPanel;
        [SerializeField] private Button closeButton;


        [Header("Gacha UI References")]
        [SerializeField] private CanvasGroup gachaCanvasGroup;
        [SerializeField] private GameObject gachaMainPanel;
        [SerializeField] private GameObject gachaResultPanel;
        [SerializeField] private TextMeshProUGUI tokenCountText;

        [Header("Gacha UI Buttons")]
        [SerializeField] private Button drawWeaponOnceButton;
        [SerializeField] private Button drawWeaponTenTimesButton;
        [SerializeField] private Button drawEngravingOnceButton;
        [SerializeField] private Button drawEngravingTenTimesButton;
        [SerializeField] private Button gachaCloseButton;
        [SerializeField] private Button resultCloseButton;

        [Header("Gacha Result Panel")]
        [SerializeField] private Transform resultSlotParent;
        [SerializeField] private GameObject resultSlotPrefab;

        [Header("Settings UI")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("QuickSlot UI")]
        [SerializeField] private QuickSlotManager quickSlotManagerPrefab;
        [SerializeField] private QuickSlotUI[] quickSlotUIArray;

        [Header("Shop UI")]
        [SerializeField] private ShopUI shopUIPrefab;
        [SerializeField] private CanvasGroup shopCanvasGroup;
        [SerializeField] private Transform shopSlotParent;
        [SerializeField] private GameObject shopSlotPrefab;
        [SerializeField] private Button shopCloseButton;
        [SerializeField] private TextMeshProUGUI shopPlayerGoldText;
        [SerializeField] private Transform shopPlayerInventoryParent;

        [Header("Menu UI")]
        [SerializeField] private CanvasGroup menuCanvasGroup;
        [SerializeField] private GameObject menuMainPanel;
        [SerializeField] private Button menuResumeButton;
        [SerializeField] private Button menuSettingsButton;
        [SerializeField] private GameObject menuSettingsPanel;
        [SerializeField] private Button menuControlButton;
        [SerializeField] private GameObject menuControlsPanel;
        [SerializeField] private Button menuMainMenuButton;

        [Header("Engraving UI")]
        [SerializeField] private EngravingUIController engravingUIControllerPrefab;
        [SerializeField] private CanvasGroup engravingCanvasGroup;

        public override void InstallBindings()
        {
            if (inventoryCanvasGroup != null)
            {
                Container.Bind<CanvasGroup>()
                    .WithId("InventoryCanvasGroup")
                    .FromInstance(inventoryCanvasGroup)
                    .AsCached();
            }
            
            if (equipmentPanel != null)
                Container.Bind<GameObject>()
                    .WithId("EquipmentPanel")
                    .FromInstance(equipmentPanel)
                    .AsCached();

            if (statsPanel != null)
                Container.Bind<GameObject>()
                    .WithId("StatsPanel")
                    .FromInstance(statsPanel)
                    .AsCached();

            if (inventorySlotParent != null)
            {
                if (shopPlayerInventoryParent == null)
                {
                    shopPlayerInventoryParent = inventorySlotParent;
                }
                Container.Bind<Transform>()
                    .WithId("InventorySlotParent")
                    .FromInstance(inventorySlotParent);
            }

            if (closeButton != null)
                Container.Bind<Button>()
                    .WithId("CloseButton")
                    .FromInstance(closeButton)
                    .AsCached();

            if (engravingGridUI != null)
                Container.Bind<EngravingGridUI>().FromInstance(engravingGridUI).AsSingle().NonLazy();

            if (engravingTooltip != null)
                Container.Bind<EngravingTooltip>().FromInstance(engravingTooltip).AsSingle();


            if (gachaCanvasGroup != null)
                Container.Bind<CanvasGroup>().WithId("GachaCanvasGroup").FromInstance(gachaCanvasGroup);

            if (gachaMainPanel != null)
                Container.Bind<GameObject>().WithId("GachaMainPanel").FromInstance(gachaMainPanel);

            if (gachaResultPanel != null)
                Container.Bind<GameObject>().WithId("GachaResultPanel").FromInstance(gachaResultPanel);

            if (tokenCountText != null)
                Container.Bind<TextMeshProUGUI>().WithId("TokenCountText").FromInstance(tokenCountText);

            if (drawWeaponOnceButton != null)
                Container.Bind<Button>().WithId("DrawWeaponOnceButton").FromInstance(drawWeaponOnceButton);

            if (drawWeaponTenTimesButton != null)
                Container.Bind<Button>().WithId("DrawWeaponTenTimesButton").FromInstance(drawWeaponTenTimesButton);

            if (drawEngravingOnceButton != null)
                Container.Bind<Button>().WithId("DrawEngravingOnceButton").FromInstance(drawEngravingOnceButton);

            if (drawEngravingTenTimesButton != null)
                Container.Bind<Button>().WithId("DrawEngravingTenTimesButton").FromInstance(drawEngravingTenTimesButton);

            if (gachaCloseButton != null)
                Container.Bind<Button>().WithId("GachaCloseButton").FromInstance(gachaCloseButton);

            if (resultCloseButton != null)
                Container.Bind<Button>().WithId("ResultCloseButton").FromInstance(resultCloseButton);

            if (resultSlotParent != null)
                Container.Bind<Transform>().WithId("ResultSlotParent").FromInstance(resultSlotParent);

            if (resultSlotPrefab != null)
                Container.Bind<GameObject>().WithId("ResultSlotPrefab").FromInstance(resultSlotPrefab);

            if (masterSlider != null)
                Container.Bind<Slider>().WithId("MasterSlider").FromInstance(masterSlider);

            if (bgmSlider != null)
                Container.Bind<Slider>().WithId("BGMSlider").FromInstance(bgmSlider);

            if (sfxSlider != null)
                Container.Bind<Slider>().WithId("SFXSlider").FromInstance(sfxSlider);

            if (fullscreenToggle != null)
                Container.Bind<Toggle>().WithId("FullscreenToggle").FromInstance(fullscreenToggle);

            if (resolutionDropdown != null)
                Container.Bind<TMP_Dropdown>().WithId("ResolutionDropdown").FromInstance(resolutionDropdown);

            if (quickSlotManagerPrefab != null)
            {
                Container.BindInterfacesAndSelfTo<QuickSlotManager>().FromComponentInNewPrefab(quickSlotManagerPrefab).AsSingle().NonLazy();
            }
            if (quickSlotUIArray != null && quickSlotUIArray.Length > 0)
            {
                Container.Bind<QuickSlotUI[]>().FromInstance(quickSlotUIArray).AsSingle();
            }


            if (shopCanvasGroup != null)
                Container.Bind<CanvasGroup>().WithId("ShopCanvasGroup").FromInstance(shopCanvasGroup);

            if (shopSlotParent != null)
                Container.Bind<Transform>().WithId("ShopSlotParent").FromInstance(shopSlotParent);

            if (shopSlotPrefab != null)
                Container.Bind<GameObject>().WithId("ShopSlotPrefab").FromInstance(shopSlotPrefab);

            if (shopCloseButton != null)
                Container.Bind<Button>().WithId("ShopCloseButton").FromInstance(shopCloseButton);

            if (shopPlayerGoldText != null)
                Container.Bind<TextMeshProUGUI>().WithId("ShopPlayerGoldText").FromInstance(shopPlayerGoldText);

            if (shopPlayerInventoryParent != null)
                Container.Bind<Transform>().WithId("ShopPlayerInventoryParent").FromInstance(shopPlayerInventoryParent);


            if (menuCanvasGroup != null)
                Container.Bind<CanvasGroup>().WithId("MenuCanvasGroup").FromInstance(menuCanvasGroup);

            if (menuMainPanel != null)
                Container.Bind<GameObject>().WithId("MenuMainPanel").FromInstance(menuMainPanel);

            if (menuResumeButton != null)
                Container.Bind<Button>().WithId("MenuResumeButton").FromInstance(menuResumeButton);

            if (menuSettingsButton != null)
                Container.Bind<Button>().WithId("MenuSettingsButton").FromInstance(menuSettingsButton);

            if (menuSettingsPanel != null)
                Container.Bind<GameObject>().WithId("MenuSettingsPanel").FromInstance(menuSettingsPanel);

            if (menuControlButton != null)
                Container.Bind<Button>().WithId("MenuControlButton").FromInstance(menuControlButton);

            if (menuControlsPanel != null)
                Container.Bind<GameObject>().WithId("MenuControlsPanel").FromInstance(menuControlsPanel);

            if (menuMainMenuButton != null)
                Container.Bind<Button>().WithId("MenuMainMenuButton").FromInstance(menuMainMenuButton);

            if (engravingCanvasGroup != null)
                Container.Bind<CanvasGroup>().WithId("EngravingCanvasGroup").FromInstance(engravingCanvasGroup);

            if (engravingUIControllerPrefab != null)
            {
                Container.Bind<EngravingUIController>().FromComponentInNewPrefab(engravingUIControllerPrefab).AsSingle().NonLazy();
            }
        }
    }
}