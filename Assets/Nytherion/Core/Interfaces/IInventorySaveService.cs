using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Nytherion.UI.Inventory;

namespace Nytherion.Interfaces
{
    public interface IInventorySaveService
    {
        void SaveInventory(InventoryState state);
        InventoryState LoadInventory();
    }
}