using System.Collections.Generic;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Shop;
using UnityEngine;

namespace Nytherion.Core.Managers
{
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        private Dictionary<string, List<ShopItemData>> runtimeShopInventories = new Dictionary<string, List<ShopItemData>>();

        [SerializeField] private List<ShopData> allShopDataAssets;

        public event System.Action OnStockChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Initialize()
        {
            foreach (var shopDataAsset in allShopDataAssets)
            {
                var runtimeItems = shopDataAsset.itemsForSale.Select(originalItem => new ShopItemData
                {
                    shopItemId = originalItem.shopItemId,
                    item = originalItem.item,
                    price = originalItem.price,
                    stock = originalItem.stock,
                    isUnlimited = originalItem.isUnlimited
                }).ToList();

                runtimeShopInventories[shopDataAsset.shopName] = runtimeItems;
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

        public List<Data.ShopStockState> GetShopStockForSave()
        {
            var stockStates = new List<Data.ShopStockState>();
            foreach (var shopInventoryPair in runtimeShopInventories)
            {
                foreach (var item in shopInventoryPair.Value)
                {
                    if (!item.isUnlimited)
                    {
                        stockStates.Add(new Data.ShopStockState
                        {
                            shopItemId = item.shopItemId,
                            remainingStock = item.stock
                        });
                    }
                }
            }
            return stockStates;
        }

        public void LoadShopStockFromSave(List<Data.ShopStockState> savedStocks)
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
    }
}