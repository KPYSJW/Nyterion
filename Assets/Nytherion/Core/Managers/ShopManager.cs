using System.Collections.Generic;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Shop;
using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Data;
using Nytherion.Core.Utils;
using Zenject;

namespace Nytherion.Core.Managers
{
    public class ShopManager : BaseManager
    {
        public bool IsShopOpen { get; private set; }
        [Header("Shop Settings")]
        [SerializeField] private List<ShopData> allShopDataAssets;

        private Dictionary<string, List<ShopItemData>> runtimeShopInventories = new();

        public event System.Action OnStockChanged;

        protected override void OnInitializeInternal()
        {
            InitializeShopInventories();
        }

        private void InitializeShopInventories()
        {
            if (allShopDataAssets == null || allShopDataAssets.Count == 0)
            {
                Debug.LogWarning("[ShopManager] No shop data assets found. Shops will be empty.");
                return;
            }

            runtimeShopInventories.Clear();

            try
            {
                foreach (var shopDataAsset in allShopDataAssets)
                {
                    if (shopDataAsset == null)
                    {
                        Debug.LogWarning("[ShopManager] Null shop data asset found. Skipping.");
                        continue;
                    }

                    var runtimeItems = shopDataAsset.itemsForSale?.Select(originalItem => new ShopItemData
                    {
                        shopItemId = originalItem.shopItemId,
                        item = originalItem.item,
                        price = originalItem.price,
                        stock = originalItem.stock,
                        isUnlimited = originalItem.isUnlimited
                    }).ToList() ?? new List<ShopItemData>();

                    runtimeShopInventories[shopDataAsset.shopName] = runtimeItems;
                    Debug.Log($"[ShopManager] Initialized shop '{shopDataAsset.shopName}' with {runtimeItems.Count} items");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ShopManager] Error initializing shop inventories: {e.Message}");
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
                    shopItem.stock--;
                }
                else
                {
                    Debug.LogWarning($"[ShopManager] Attempted to buy '{shopItem.item.itemName}' but stock is zero.");
                }
                OnStockChanged?.Invoke();
            }
            else
            {
                Debug.LogWarning($"[ShopManager] Shop '{shopName}' not found in runtimeShopInventories.");
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
            if (savedStocks == null) return;

            foreach (var savedItem in savedStocks)
            {
                if (string.IsNullOrEmpty(savedItem.shopItemId)) continue;

                foreach (var shopInventory in runtimeShopInventories.Values)
                {
                    var targetItem = shopInventory.FirstOrDefault(i => i.shopItemId == savedItem.shopItemId);
                    if (targetItem != null)
                    {
                        targetItem.stock = savedItem.remainingStock;
                        break;
                    }
                }
            }
            OnStockChanged?.Invoke();
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            SaveLoadHelper.SafePopulateCollection(
                saveData,
                GetShopStockForSave(),
                (data, stocks) => data.shopStockData = stocks,
                nameof(ShopManager)
            );
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            SaveLoadHelper.SafeLoadCollection(
                saveData,
                data => data?.shopStockData,
                LoadShopStockFromSave,
                nameof(ShopManager)
            );
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