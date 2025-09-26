using System.Collections.Generic;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Shop;
using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Data;
using Nytherion.Core.Utils;
using VContainer;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public class ShopManager : BaseManager
    {
        public bool IsShopOpen { get; private set; }
        [Header("Shop Settings")]
        [SerializeField] private List<ShopData> allShopDataAssets;

        private Dictionary<string, List<ShopItemData>> runtimeShopInventories = new();
        private bool hasLoadedSaveData = false;

        public event System.Action OnStockChanged;

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

                        var runtimeItems = shopDataAsset.itemsForSale?.Select(originalItem => {
                            return new ShopItemData
                            {
                                shopItemId = originalItem.shopItemId,
                                item = originalItem.item,
                                price = originalItem.price,
                                stock = originalItem.stock,
                                isUnlimited = originalItem.isUnlimited
                            };
                        }).ToList() ?? new List<ShopItemData>();

                        runtimeShopInventories[shopDataAsset.shopName] = runtimeItems;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ShopManager] Error initializing shop inventories: {e.Message}");
                }
            }
        }

        public List<ShopItemData> GetShopItems(string shopName)
        {
            if (runtimeShopInventories.TryGetValue(shopName, out var items))
            {
                return items;
            }
            return null;
        }

        public void RecordPurchase(string shopName, string shopItemId)
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
                    int oldStock = shopItem.stock;
                    shopItem.stock--;

                    OnStockChanged?.Invoke();

                    // 구매 시 자동 저장 트리거
                    var saveLoadManager = FindObjectOfType<SaveLoadManager>();
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
                    if (!item.isUnlimited)
                    {
                        stockStates.Add(new ShopStockState
                        {
                            shopItemId = item.shopItemId,
                            remainingStock = item.stock
                        });
                    }
                }
            }
            return stockStates;
        }

        private void LoadShopStockFromSave(List<ShopStockState> savedStocks)
        {
            if (savedStocks == null)
            {
                Debug.Log("[ShopManager] LoadShopStockFromSave: savedStocks가 null");
                return;
            }

            int updatedCount = 0;
            foreach (var savedItem in savedStocks)
            {
                if (string.IsNullOrEmpty(savedItem.shopItemId))
                {
                    continue;
                }

                bool found = false;
                foreach (var shopInventoryPair in runtimeShopInventories)
                {
                    foreach (var targetItem in shopInventoryPair.Value)
                    {
                        // ID 비교를 더 엄격하게 수행
                        if (!string.IsNullOrEmpty(targetItem.shopItemId) &&
                            targetItem.shopItemId.Equals(savedItem.shopItemId, System.StringComparison.Ordinal))
                        {
                            int oldStock = targetItem.stock;
                            targetItem.stock = savedItem.remainingStock;
                            updatedCount++;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                if (!found)
                {
                }
            }

            OnStockChanged?.Invoke();
        }

        public override void PopulateSaveData(SaveData saveData)
        {
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
            // 항상 기본 데이터로 먼저 초기화
            InitializeShopInventories(true);

            // 저장된 데이터가 있다면 적용
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
        /// <summary>
        /// 특정 상점이 존재하는지 확인합니다.
        /// </summary>
        public bool ShopExists(string shopName)
        {
            return !string.IsNullOrEmpty(shopName) && runtimeShopInventories.ContainsKey(shopName);
        }

        /// <summary>
        /// 등록된 모든 상점의 이름을 반환합니다.
        /// </summary>
        public List<string> GetAllShopNames()
        {
            return new List<string>(runtimeShopInventories.Keys);
        }

        /// <summary>
        /// 특정 아이템이 매진되었는지 확인합니다.
        /// </summary>
        public bool IsItemSoldOut(string shopName, string shopItemId)
        {
            if (!ShopExists(shopName)) return true;

            var shopItem = runtimeShopInventories[shopName]
                .FirstOrDefault(item => item.shopItemId == shopItemId);

            return shopItem != null && !shopItem.isUnlimited && shopItem.stock <= 0;
        }

        /// <summary>
        /// 상점 상태 정보를 반환합니다.
        /// </summary>
        public override string GetStatusInfo()
        {
            int totalShops = runtimeShopInventories.Count;
            int totalItems = runtimeShopInventories.Values.Sum(items => items.Count);

            return $"{base.GetStatusInfo()}, Shops: {totalShops}, Total Items: {totalItems}";
        }

        /// <summary>
        /// 메모리 정리
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            OnStockChanged = null;
            runtimeShopInventories?.Clear();
        }
    }
}