using UnityEngine;
using Nytherion.Services;
using Nytherion.Core.Data;

namespace Nytherion.Core.Managers
{
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; private set; }

        private JsonSaveService saveService;
        private SaveData saveData;
        private bool isLoadingData = false;
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

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.onCurrencyChanged += OnCurrencyChanged;
            }
            if (EngravingManager.Instance != null)
            {
                EngravingManager.Instance.OnEngravingStateChanged += OnDataChanged;
            }
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += OnDataChanged;
            }
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnStockChanged += OnDataChanged;
            }
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

            CurrencyManager.Instance.GetCurrenciesForSave(saveData);
            saveData.inventoryData = InventoryManager.Instance.GetInventoryForSave();
            saveData.engravingData = EngravingManager.Instance.GetEngravingsForSave();

            if (ShopManager.Instance != null)
            {
                saveData.shopStockData = ShopManager.Instance.GetShopStockForSave();
            }

            saveService.Save(saveData);
        }


        public void LoadGame()
        {
            isLoadingData = true;

            saveData = saveService.Load();
            if (saveData == null)
            {
                saveData = new SaveData();
                Debug.Log("<color=orange>[SaveLoadManager] 저장 파일이 없어 새 데이터를 생성합니다.</color>");
            }

            CurrencyManager.Instance.LoadDataFromSave(saveData);
            InventoryManager.Instance.LoadDataFromSave(saveData.inventoryData);
            EngravingManager.Instance.LoadDataFromSave(saveData.engravingData);

            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.LoadShopStockFromSave(saveData.shopStockData);
            }

            Debug.Log("<color=lime>[SaveLoadManager] 저장된 데이터를 모두 불러왔습니다.</color>");

            isLoadingData = false;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveGame();
            }
        }

        private void OnApplicationQuit()
        {
            SaveGame();
        }

        private void OnDestroy()
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.onCurrencyChanged -= OnCurrencyChanged;
            }
            if (EngravingManager.Instance != null)
            {
                EngravingManager.Instance.OnEngravingStateChanged -= OnDataChanged;
            }
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated -= OnDataChanged;
            }
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnStockChanged -= OnDataChanged;
            }
        }
    }
}