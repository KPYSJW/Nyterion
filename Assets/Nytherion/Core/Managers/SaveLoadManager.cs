using UnityEngine;
using Nytherion.Services;
using Nytherion.Core.Data;
using Nytherion.UI.Inventory;
using Nytherion.GamePlay.Characters.Player;
using System.Collections;
using Zenject; // Zenject 네임스페이스 추가

namespace Nytherion.Core.Managers
{
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; private set; }

        private JsonSaveService saveService;
        private SaveData saveData;
        private bool isLoadingData = false;
        private Coroutine saveCoroutine;

        private PlayerManager playerManager;
        private InventoryManager inventoryManager;
        private EngravingManager engravingManager;
        private ShopManager shopManager;
        private QuickSlotManager quickSlotManager;
        private CurrencyManager currencyManager;
        private EquipmentDataManager equipmentManager;

        [Inject]
        public void Construct(
            PlayerManager playerManager,
            InventoryManager inventoryManager,
            EngravingManager engravingManager,
            ShopManager shopManager,
            QuickSlotManager quickSlotManager,
            CurrencyManager currencyManager,
            EquipmentDataManager equipmentManager)
        {
            this.playerManager = playerManager;
            this.inventoryManager = inventoryManager;
            this.engravingManager = engravingManager;
            this.shopManager = shopManager;
            this.quickSlotManager = quickSlotManager;
            this.currencyManager = currencyManager;
            this.equipmentManager = equipmentManager;
        }

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
            if (isLoadingData) return;
            if (saveData == null) saveData = new SaveData();

            if (inventoryManager != null)
            {
                saveData.inventoryData = inventoryManager.GetInventoryForSave();
            }

            if (equipmentManager != null)
            {
                saveData.equippedItemsData = equipmentManager.GetEquipmentForSave();
            }

            if (quickSlotManager != null)
            {
                quickSlotManager.GetStateForSave(saveData);
            }

            if (currencyManager != null)
            {
                currencyManager.GetCurrenciesForSave(saveData);
            }

            if (engravingManager != null)
            {
                saveData.engravingData = engravingManager.GetEngravingsForSave();
            }

            if (shopManager != null)
            {
                saveData.shopStockData = shopManager.GetShopStockForSave();
            }

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