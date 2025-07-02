using Nytherion.UI.Inventory;
using Nytherion.Data.ScriptableObjects.Engravings;
using System.Collections.Generic;

namespace Nytherion.Core
{
    [System.Serializable]
    public class SaveData
    {
        public Dictionary<CurrencyType, int> currencyData;
        public InventoryState inventoryData;

        public EngravingGridState engravingData;

        public SaveData()
        {
            currencyData = new Dictionary<CurrencyType, int>();
            inventoryData = new InventoryState();
            engravingData = new EngravingGridState();
        }
    }
}