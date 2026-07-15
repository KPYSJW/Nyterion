using Nytherion.Core.Data;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.Core.Systems;
using Nytherion.Core.Enums;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace Nytherion.Core.Managers
{
    public class ShopManager : BaseManager
    {
        public bool IsShopOpen { get; private set; }
        [Header("Shop Settings")]
        [SerializeField] private List<ShopData> allShopDataAssets;

        private Dictionary<string, List<ShopItemData>> runtimeShopInventories = new();
        private bool hasLoadedSaveData = false;

        private CurrencyDataManager currencyDataManager;
        private SaveLoadManager saveLoadManager;
        private int rerollCount = 0;

        public int CurrentRerollCost => 100 + (rerollCount * 50);
        public int RerollCount => rerollCount;

        public event System.Action OnStockChanged;

        [Inject]
        public void Construct(CurrencyDataManager currencyDataManager, SaveLoadManager saveLoadManager)
        {
            this.currencyDataManager = currencyDataManager;
            this.saveLoadManager = saveLoadManager;
        }

        protected override void OnInitializeInternal()
        {
            InitializeShopInventories();
        }

        private void InitializeShopInventories(bool forceInit = false)
        {
            if (allShopDataAssets == null || allShopDataAssets.Count == 0)
            {
                return;
            }

            // 강제 초기화이거나 저장 데이터가 로드되지 않았을 때만 초기화
            if (forceInit || !hasLoadedSaveData)
            {
                runtimeShopInventories.Clear();

                try
                {
                    foreach (var shopDataAsset in allShopDataAssets)
                    {
                        if (shopDataAsset == null)
                        {
                            continue;
                        }

                        // 최초 로딩 시 무료(isFree=true)로 5개 장비 무작위 리롤 생성하여 진열
                        RerollShop(shopDataAsset.shopName, true);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ShopManager] Error initializing shop inventories: {e.Message}");
                }
            }
        }

        public void ResetRerollCount()
        {
            rerollCount = 0;
            OnStockChanged?.Invoke();
        }

        public bool RerollShop(string shopName, bool isFree = false)
        {
            if (string.IsNullOrEmpty(shopName)) return false;

            if (!isFree)
            {
                if (currencyDataManager == null)
                {
                    Debug.LogError("[ShopManager] CurrencyDataManager가 주입되지 않았습니다.");
                    return false;
                }

                int cost = CurrentRerollCost;
                if (currencyDataManager.GetCurrency(CurrencyType.Gold) < cost)
                {
                    Debug.LogWarning($"[ShopManager] 골드 부족. 현재 골드: {currencyDataManager.GetCurrency(CurrencyType.Gold)}, 필요 골드: {cost}");
                    return false;
                }

                currencyDataManager.SpendCurrency(CurrencyType.Gold, cost);
                rerollCount++;
            }

            // 장비 아이템 데이터베이스 가져오기
            List<EquipmentData> equipmentPool = ItemDatabase.GetAllItems()
                .OfType<EquipmentData>()
                .ToList();

            if (equipmentPool.Count == 0)
            {
                Debug.LogError("[ShopManager] ItemDatabase에 사용 가능한 장비(EquipmentData)가 없습니다.");
                return false;
            }

            List<ShopItemData> newItems = new List<ShopItemData>();

            // 등급 확률 가중치 설정 (Common 50%, Uncommon 30%, Rare 13%, Epic 5%, Legendary 2%)
            Dictionary<Rarity, int> rarityWeights = new Dictionary<Rarity, int>
            {
                { Rarity.Common, 50 },
                { Rarity.Uncommon, 30 },
                { Rarity.Rare, 13 },
                { Rarity.Epic, 5 },
                { Rarity.Legendary, 2 }
            };

            int totalWeight = 0;
            foreach (KeyValuePair<Rarity, int> pair in rarityWeights)
            {
                totalWeight += pair.Value;
            }

            // 상품 진열 시 중복 장비를 방지하기 위한 해시셋
            HashSet<int> selectedIndices = new HashSet<int>();

            for (int i = 0; i < 5; i++)
            {
                // 등급 추첨
                int randomVal = Random.Range(0, totalWeight);
                Rarity chosenRarity = Rarity.Common;
                int currentSum = 0;
                foreach (KeyValuePair<Rarity, int> pair in rarityWeights)
                {
                    currentSum += pair.Value;
                    if (randomVal < currentSum)
                    {
                        chosenRarity = pair.Key;
                        break;
                    }
                }

                // 무작위 장비 선택 (중복 제거하되 풀이 부족하면 중복 허용)
                int poolIndex = -1;
                int attempts = 0;
                do
                {
                    poolIndex = Random.Range(0, equipmentPool.Count);
                    attempts++;
                } while (selectedIndices.Contains(poolIndex) && selectedIndices.Count < equipmentPool.Count && attempts < 100);

                selectedIndices.Add(poolIndex);
                EquipmentData originalEquipment = equipmentPool[poolIndex];

                // 장비 인스턴스 복제 및 등급별 스탯과 가격 보정 적용
                EquipmentData clonedEquipment = Instantiate(originalEquipment);
                clonedEquipment.instanceId = System.Guid.NewGuid().ToString();
                clonedEquipment.ApplyRarityStats(chosenRarity);

                ShopItemData shopItem = new ShopItemData
                {
                    shopItemId = System.Guid.NewGuid().ToString(),
                    item = clonedEquipment,
                    price = clonedEquipment.baseValue,
                    stock = 1,
                    isUnlimited = false
                };

                newItems.Add(shopItem);
            }

            runtimeShopInventories[shopName] = newItems;

            OnStockChanged?.Invoke();

            if (saveLoadManager != null && !isFree)
            {
                saveLoadManager.SaveGame();
            }

            return true;
        }

        public List<ShopItemData> GetShopItems(string shopName)
        {
            if (runtimeShopInventories.TryGetValue(shopName, out var items))
            {
                return items;
            }
            return null;
        }

        public void RecordPurchase(string shopName, string shopItemId, int amountToBuy = 1)
        {
            if (runtimeShopInventories.TryGetValue(shopName, out var items))
            {
                var shopItem = items.FirstOrDefault(i => i.shopItemId == shopItemId);
                if (shopItem == null)
                {
                    return;
                }

                if (shopItem.isUnlimited)
                {
                    return;
                }

                if (shopItem.stock > 0)
                {
                    shopItem.stock--;

                    OnStockChanged?.Invoke();

                    if (saveLoadManager != null)
                    {
                        saveLoadManager.SaveGame();
                    }
                }
                else
                {
                    Debug.LogWarning($"[ShopManager] '{shopItem.item.itemName}' 재고가 이미 0입니다. 구매 불가.");
                }
            }
        }

        private List<ShopStockState> GetShopStockForSave()
        {
            var stockStates = new List<ShopStockState>();
            foreach (var shopInventoryPair in runtimeShopInventories)
            {
                foreach (var item in shopInventoryPair.Value)
                {
                    stockStates.Add(new ShopStockState
                    {
                        shopName = shopInventoryPair.Key,
                        shopItemId = item.shopItemId,
                        itemId = item.item.ID,
                        rarity = (item.item is EquipmentData eq) ? eq.rarity : Rarity.Common,
                        price = item.price,
                        remainingStock = item.stock,
                        isUnlimited = item.isUnlimited
                    });
                }
            }
            return stockStates;
        }

        private void LoadShopStockFromSave(List<ShopStockState> savedStocks)
        {
            if (savedStocks == null || savedStocks.Count == 0)
            {
                Debug.Log("[ShopManager] LoadShopStockFromSave: savedStocks가 비어있음");
                return;
            }

            runtimeShopInventories.Clear();

            foreach (var savedItem in savedStocks)
            {
                if (string.IsNullOrEmpty(savedItem.shopName) || string.IsNullOrEmpty(savedItem.itemId))
                {
                    continue;
                }

                ItemData itemAsset = ItemDatabase.GetItemByID(savedItem.itemId);
                if (itemAsset == null)
                {
                    Debug.LogWarning($"[ShopManager] 로드 실패: ItemDatabase에 아이템({savedItem.itemId})이 없습니다.");
                    continue;
                }

                ItemData itemInstance = itemAsset;

                if (itemAsset is EquipmentData equipmentAsset)
                {
                    EquipmentData clonedEquipment = Instantiate(equipmentAsset);
                    clonedEquipment.instanceId = System.Guid.NewGuid().ToString();
                    clonedEquipment.ApplyRarityStats(savedItem.rarity);
                    itemInstance = clonedEquipment;
                }

                ShopItemData restoredItem = new ShopItemData
                {
                    shopItemId = savedItem.shopItemId,
                    item = itemInstance,
                    price = savedItem.price,
                    stock = savedItem.remainingStock,
                    isUnlimited = savedItem.isUnlimited
                };

                if (!runtimeShopInventories.ContainsKey(savedItem.shopName))
                {
                    runtimeShopInventories[savedItem.shopName] = new List<ShopItemData>();
                }
                runtimeShopInventories[savedItem.shopName].Add(restoredItem);
            }

            OnStockChanged?.Invoke();
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            saveData.shopRerollCount = rerollCount;
            var stockData = GetShopStockForSave();

            SaveLoadHelper.SafePopulateCollection(
                saveData,
                stockData,
                (data, stocks) => data.shopStockData = stocks,
                nameof(ShopManager)
            );
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            if (saveData != null)
            {
                rerollCount = saveData.shopRerollCount;
            }

            InitializeShopInventories(true);

            if (saveData?.shopStockData != null && saveData.shopStockData.Count > 0)
            {
                SaveLoadHelper.SafeLoadCollection(
                    saveData,
                    data => data?.shopStockData,
                    LoadShopStockFromSave,
                    nameof(ShopManager)
                );
            }

            hasLoadedSaveData = true;
        }

        public void SetShopState(bool isOpen)
        {
            IsShopOpen = isOpen;
        }

        public bool ShopExists(string shopName)
        {
            return !string.IsNullOrEmpty(shopName) && runtimeShopInventories.ContainsKey(shopName);
        }

        public List<string> GetAllShopNames()
        {
            return new List<string>(runtimeShopInventories.Keys);
        }

        public bool IsItemSoldOut(string shopName, string shopItemId)
        {
            if (!ShopExists(shopName)) return true;

            var shopItem = runtimeShopInventories[shopName]
                .FirstOrDefault(item => item.shopItemId == shopItemId);

            return shopItem != null && !shopItem.isUnlimited && shopItem.stock <= 0;
        }

        public override string GetStatusInfo()
        {
            int totalShops = runtimeShopInventories.Count;
            int totalItems = runtimeShopInventories.Values.Sum(items => items.Count);

            return $"{base.GetStatusInfo()}, Shops: {totalShops}, Total Items: {totalItems}";
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            OnStockChanged = null;
            runtimeShopInventories?.Clear();
        }
    }
}