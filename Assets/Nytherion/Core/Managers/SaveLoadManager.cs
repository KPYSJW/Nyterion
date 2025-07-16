using UnityEngine;
using Nytherion.Services;
using Nytherion.Core.Data;
using Nytherion.UI.Inventory;
using Nytherion.GamePlay.Characters.Player;


namespace Nytherion.Core.Managers
{
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; private set; }

        private JsonSaveService saveService;
        private SaveData saveData;
        private bool isLoadingData = false;

        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private EngravingManager engravingManager;
        [SerializeField] private ShopManager shopManager;
        [SerializeField] private QuickSlotManager quickSlotManager;
        [SerializeField] private CurrencyManager currencyManager;
        [SerializeField] private EquipmentDataManager equipmentManager; // 추가: EquipmentDataManager 참조

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                saveService = new JsonSaveService();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Initialize()
        {
            LoadGame();

            if (currencyManager != null) currencyManager.onCurrencyChanged += OnCurrencyChanged;
            if (engravingManager != null) engravingManager.OnEngravingStateChanged += OnDataChanged;
            if (inventoryManager != null) inventoryManager.OnInventoryUpdated += OnDataChanged;
            if (shopManager != null) shopManager.OnStockChanged += OnDataChanged;
        }

        private void OnDataChanged()
        {
            if (isLoadingData) return;
            SaveGame();
        }

        private void OnCurrencyChanged(CurrencyType type, int amount)
        {
            OnDataChanged();
        }

        public void SaveGame()
        {
            if (isLoadingData) return;
            if (saveData == null) saveData = new SaveData();

            saveData.inventoryData = inventoryManager.GetInventoryForSave();

            saveData.equippedItemsData = equipmentManager.GetEquipmentForSave();

            quickSlotManager.GetStateForSave(saveData);

            currencyManager.GetCurrenciesForSave(saveData);
            saveData.engravingData = engravingManager.GetEngravingsForSave();
            saveData.shopStockData = shopManager.GetShopStockForSave();

            saveService.Save(saveData);
        }

        public void LoadGame()
        {
            isLoadingData = true;

            saveData = saveService.Load() ?? new SaveData();

            inventoryManager.LoadDataFromSave(saveData.inventoryData);

            equipmentManager.LoadEquipmentFromSave(saveData.equippedItemsData);

            quickSlotManager.LoadStateFromSave(saveData);

            currencyManager.LoadDataFromSave(saveData);
            engravingManager.LoadDataFromSave(saveData.engravingData);
            shopManager.LoadShopStockFromSave(saveData.shopStockData);

            inventoryManager.TriggerInventoryUpdate();

            isLoadingData = false;
        }

        private void OnApplicationQuit()
        {
            if (!isLoadingData)
            {
                SaveGame();
            }
        }

        private void OnDestroy()
        {
            if (currencyManager != null) currencyManager.onCurrencyChanged -= OnCurrencyChanged;
            if (engravingManager != null) engravingManager.OnEngravingStateChanged -= OnDataChanged;
            if (inventoryManager != null) inventoryManager.OnInventoryUpdated -= OnDataChanged;
            if (shopManager != null) shopManager.OnStockChanged -= OnDataChanged;
        }
    }
}