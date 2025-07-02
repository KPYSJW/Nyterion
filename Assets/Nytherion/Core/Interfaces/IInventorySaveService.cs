using Nytherion.Core.Data;

namespace Nytherion.Core.Interfaces
{
    public interface IInventorySaveService
    {
        void SaveInventory(InventoryState state);
        InventoryState LoadInventory();
    }
}