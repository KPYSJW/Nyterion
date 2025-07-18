using System.Collections.Generic;
using UnityEngine;
using System;
using Nytherion.Core.Data;

namespace Nytherion.Core.Managers
{
    public enum CurrencyType { Gold = 0, Token = 1 }
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }
        public event Action OnInitialized;
        private Dictionary<CurrencyType, int> currencies = new();
        public event Action<CurrencyType, int> onCurrencyChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent != null) transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        public void Initialize()
        {
            OnInitialized?.Invoke();
        }
        public void GetCurrenciesForSave(SaveData saveData)
        {
            saveData.currencyTypes.Clear();
            saveData.currencyAmounts.Clear();
            foreach (var currencyPair in currencies)
            {
                saveData.currencyTypes.Add(currencyPair.Key);
                saveData.currencyAmounts.Add(currencyPair.Value);
            }
        }
        public void LoadDataFromSave(SaveData data)
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
        }
        public bool SpendCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) return false;
            if (GetCurrency(type) >= amount)
            {
                currencies[type] -= amount;
                onCurrencyChanged?.Invoke(type, currencies[type]);
                return true;
            }
            return false;
        }

    }
}