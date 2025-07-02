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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                saveService = new JsonSaveService();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SaveGame()
        {
            if (saveData == null) saveData = new SaveData();

            saveData.currencyData = CurrencyManager.Instance.GetCurrenciesForSave();
            saveData.inventoryData = InventoryManager.Instance.GetInventoryForSave();
            saveData.engravingData = EngravingManager.Instance.GetEngravingsForSave();

            saveService.Save(saveData);
            Debug.Log("<color=yellow>[SaveLoadManager] Game Saved!</color>");
        }

        public void LoadGame()
        {
            saveData = saveService.Load();

            CurrencyManager.Instance.LoadDataFromSave(saveData.currencyData);
            InventoryManager.Instance.LoadDataFromSave(saveData.inventoryData);
            EngravingManager.Instance.LoadDataFromSave(saveData.engravingData);
            Debug.Log("<color=green>[SaveLoadManager] Game Loaded!</color>");
        }

        public void Initialize()
        {
            LoadGame();
        }
    }
}