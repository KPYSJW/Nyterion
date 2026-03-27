using Nytherion.Core.Data;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Shop;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nytherion.Core.Managers
{
    public class ShopManager : BaseManager
    {
        public bool IsShopOpen { get; private set; }
        [Header("Shop Settings")]
        [SerializeField] private List<ShopData> allShopDataAssets;

        private Dictionary<string, List<ShopItemData>> runtimeShopInventories = new();
        private bool hasLoadedSaveData = false;

        [Header("Buyback Settings")]
        [SerializeField] private int maxBuybackItems = 15; 
        private List<ShopItemData> buybackInventory = new List<ShopItemData>();

        public event System.Action OnBuybackChanged;
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

                        var runtimeItems = shopDataAsset.itemsForSale?.Select(originalItem =>
                        {
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

        /// <summary>
        /// 판매한 아이템을 재구매 목록에 추가
        /// </summary>
        public void AddToBuyback(ItemData item, int amount, int soldPricePerItem)
        {
            // 동일한 아이템이 같은 가격으로 존재하는지 확인
            var existingItem = buybackInventory.FirstOrDefault(i => i.item.ID == item.ID && i.price == soldPricePerItem);

            if (existingItem != null)
            {
                existingItem.stock += amount;
            }
            else
            {
                // 새 항목 생성 
                ShopItemData newBuybackItem = new ShopItemData
                {
                    shopItemId = System.Guid.NewGuid().ToString(), // 재구매용 고유 ID 발급
                    item = item,
                    price = soldPricePerItem, // 팔았던 가격을 그대로 구매가로 설정
                    stock = amount,
                    isUnlimited = false
                };

                // 최신 항목이 맨 위에 오도록 삽입
                buybackInventory.Insert(0, newBuybackItem);
            }

            // 최대 개수 초과 시 가장 오래된 항목 삭제
            if (buybackInventory.Count > maxBuybackItems)
            {
                buybackInventory.RemoveAt(buybackInventory.Count - 1);
            }

            OnBuybackChanged?.Invoke();
        }

        /// <summary>
        /// 현재 재구매 가능한 아이템 목록을 반환합니다.
        /// </summary>
        public List<ShopItemData> GetBuybackItems()
        {
            return buybackInventory;
        }

        /// <summary>
        /// 유저가 재구매를 완료했을 때 목록에서 수량을 차감하거나 완전히 제거합니다.
        /// </summary>
        public void RecordBuybackPurchase(string shopItemId, int amountToBuy = 1)
        {
            var item = buybackInventory.FirstOrDefault(i => i.shopItemId == shopItemId);
            if (item != null)
            {
                item.stock -= amountToBuy;
                if (item.stock <= 0)
                {
                    buybackInventory.Remove(item);
                }
                OnBuybackChanged?.Invoke();
            }
        }

        /// <summary>
        /// 상점을 닫거나 씬 이동 시 재구매 목록을 비우고 싶을 때 호출
        /// </summary>
        public void ClearBuyback()
        {
            if (buybackInventory.Count > 0)
            {
                buybackInventory.Clear();
                OnBuybackChanged?.Invoke();
            }
        }
        public void RecordPurchase(string shopName, string shopItemId,int amountToBuy = 1)
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