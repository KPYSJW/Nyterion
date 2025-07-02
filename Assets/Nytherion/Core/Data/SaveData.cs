using Nytherion.Data.ScriptableObjects.Engravings;
using System.Collections.Generic;
using Nytherion.UI.Inventory;
using Nytherion.Core.Managers;

namespace Nytherion.Core.Data
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