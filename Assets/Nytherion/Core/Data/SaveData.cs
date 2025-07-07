using System.Collections.Generic;
using Nytherion.Core.Managers;

namespace Nytherion.Core.Data
{
    [System.Serializable]
    public class ShopStockState
    {
        public string shopItemId; 
        public int remainingStock;
    }
    [System.Serializable]
    public class SaveData
    {
        public List<CurrencyType> currencyTypes = new List<CurrencyType>();
        public List<int> currencyAmounts = new List<int>();
        public InventoryState inventoryData;

        public EngravingGridState engravingData;
        public List<ShopStockState> shopStockData = new List<ShopStockState>();

        public SaveData()
        {
            currencyTypes = new List<CurrencyType>();
            currencyAmounts = new List<int>();
            inventoryData = new InventoryState();
            engravingData = new EngravingGridState();
        }
    }
}