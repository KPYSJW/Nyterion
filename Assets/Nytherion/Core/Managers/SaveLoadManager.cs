using UnityEngine;
using Nytherion.Services;
using Nytherion.Core.Data;
using Nytherion.UI.Inventory;
using Nytherion.GamePlay.Characters.Player;
using System.Collections;

namespace Nytherion.Core.Managers
{
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; private set; }

        private JsonSaveService saveService;
        private SaveData saveData;
        private bool isLoadingData = false;
        private Coroutine saveCoroutine;
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private EngravingManager engravingManager;
        [SerializeField] private ShopManager shopManager;
        [SerializeField] private QuickSlotManager quickSlotManager;
        [SerializeField] private CurrencyManager currencyManager;
        [SerializeField] private EquipmentDataManager equipmentManager;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                saveService = new JsonSaveService();
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);
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
            if (quickSlotManager != null) quickSlotManager.OnQuickSlotUpdated += OnDataChanged;
        }

        private void OnDataChanged()
        {
            if (isLoadingData) return;

            if (saveCoroutine != null)
            {
                StopCoroutine(saveCoroutine);
            }
            saveCoroutine = StartCoroutine(DelayedSave());
        }

        private IEnumerator DelayedSave()
        {
            yield return new WaitForEndOfFrame();

            SaveGame();

            saveCoroutine = null;
        }
        private void OnCurrencyChanged(CurrencyType type, int amount)
        {
            OnDataChanged();
        }

        public void SaveGame()
        {
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
            if (quickSlotManager != null) quickSlotManager.OnQuickSlotUpdated -= OnDataChanged;
        }
    }
}