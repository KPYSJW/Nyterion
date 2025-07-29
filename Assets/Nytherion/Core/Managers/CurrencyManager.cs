using System.Collections.Generic;
using UnityEngine;
using System;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Zenject;

namespace Nytherion.Core.Managers
{
    public enum CurrencyType { Gold = 0, Token = 1 }
    public class CurrencyManager : MonoBehaviour, ISaveable
    {
        public event Action OnInitialized;
        private Dictionary<CurrencyType, int> currencies = new();
        public event Action<CurrencyType, int> onCurrencyChanged;
        private SaveLoadManager saveLoadManager;

        [Inject]
        public void Construct(SaveLoadManager saveLoadManager)
        {
            this.saveLoadManager = saveLoadManager;
        }
        
        public void Initialize()
        {
            OnInitialized?.Invoke();
        }
        public void PopulateSaveData(SaveData saveData)
        {
            saveData.currencyTypes.Clear();
            saveData.currencyAmounts.Clear();
            foreach (var currencyPair in currencies)
            {
                saveData.currencyTypes.Add(currencyPair.Key);
                saveData.currencyAmounts.Add(currencyPair.Value);
            }
        }
        public void LoadFromSaveData(SaveData data)
        {
            currencies = new Dictionary<CurrencyType, int>();
            if (data == null || data.currencyTypes.Count != data.currencyAmounts.Count)
            {
                foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
                {
                    currencies[type] = 0;
                }
            }
            else
            {
                for (int i = 0; i < data.currencyTypes.Count; i++)
                {
                    currencies[data.currencyTypes[i]] = data.currencyAmounts[i];
                }
            }

            foreach (var currencyPair in currencies)
            {
                onCurrencyChanged?.Invoke(currencyPair.Key, currencyPair.Value);
            }
        }
        public int GetCurrency(CurrencyType type)
        {
            return currencies.TryGetValue(type, out int amount) ? amount : 0;
        }
        public void AddCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) return;
            currencies[type] = GetCurrency(type) + amount;
            onCurrencyChanged?.Invoke(type, currencies[type]);
            saveLoadManager.SaveGame();
        }
        public bool SpendCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) return false;
            if (GetCurrency(type) >= amount)
            {
                currencies[type] -= amount;
                onCurrencyChanged?.Invoke(type, currencies[type]);
                saveLoadManager.SaveGame();
                return true;
            }
            return false;
        }

    }
}