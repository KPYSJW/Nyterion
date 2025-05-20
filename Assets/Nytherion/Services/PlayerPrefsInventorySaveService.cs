using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.UI.Inventory;
using Nytherion.Interfaces;
using System.Threading.Tasks;

namespace Nytherion.Services
{
    public class PlayerPrefsInventorySaveService : IInventorySaveService
    {
        private const string SAVE_KEY = "InventoryState";

        public void SaveInventory(InventoryState state)
        {
            string json = JsonUtility.ToJson(state);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
        }

        public InventoryState LoadInventory()
        {
            if (PlayerPrefs.HasKey(SAVE_KEY))
            {
                string json = PlayerPrefs.GetString(SAVE_KEY);
                return JsonUtility.FromJson<InventoryState>(json);
            }
            return new InventoryState();
        }
    }
}