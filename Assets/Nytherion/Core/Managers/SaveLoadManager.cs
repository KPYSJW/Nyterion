using UnityEngine;
using Nytherion.Services;
using Nytherion.Core.Data;
using Nytherion.UI.Inventory;
using Nytherion.GamePlay.Characters.Player;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Items;
using System.Collections.Generic;

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

            currencyManager.GetCurrenciesForSave(saveData);
            
            var allItemsToSave = inventoryManager.GetInventoryForSave();
            var equippedItemsToSave = new List<ItemEntry>();
            foreach(var pair in playerManager.EquippedItems)
            {
                if(pair.Value != null)
                {
                    equippedItemsToSave.Add(new ItemEntry {
                        ItemId = pair.Value.ID,
                        Count = 1,
                        InstanceId = pair.Value.instanceId
                    });
                }
            }
            saveData.inventoryData = allItemsToSave.Concat(equippedItemsToSave).ToList();

            saveData.engravingData = engravingManager.GetEngravingsForSave();
            saveData.shopStockData = shopManager.GetShopStockForSave();
            quickSlotManager.GetStateForSave(saveData);

            saveData.equippedItemsData.Clear();
            foreach (var pair in playerManager.EquippedItems)
            {
                if (pair.Value != null)
                {
                    saveData.equippedItemsData.Add(new EquippedItemEntry
                    {
                        slotType = pair.Key,
                        instanceId = pair.Value.instanceId
                    });
                }
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

            currencyManager.LoadDataFromSave(saveData);
            inventoryManager.LoadDataFromSave(saveData.inventoryData);
            engravingManager.LoadDataFromSave(saveData.engravingData);
            shopManager.LoadShopStockFromSave(saveData.shopStockData);
            
            if (saveData.equippedItemsData != null)
            {
                var allLoadedItems = inventoryManager.InventoryModel.Items.ToList();
                foreach (var entry in saveData.equippedItemsData)
                {
                    EquipmentData itemToEquip = allLoadedItems
                        .FirstOrDefault(item => item.instanceId == entry.instanceId) as EquipmentData;

                    if (itemToEquip != null)
                    {
                        playerManager.EquipItem(entry.slotType, itemToEquip);
                        inventoryManager.RemoveItem(itemToEquip); 
                    }
                }
            }

            quickSlotManager.LoadStateFromSave(saveData);
            
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