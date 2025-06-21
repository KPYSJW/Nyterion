using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Nytherion.Core.Interfaces;
using Nytherion.Services;

namespace Nytherion.Core
{
    public enum CurrencyType
    {
        Gold = 0,
        Token = 1
    }
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }
        public event Action OnInitialized;
        private Dictionary<CurrencyType, int> currencies = new();
        public event Action<CurrencyType, int> onCurrencyChanged;

        private ICurrencySaveService saveService;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        public void Initialize()
        {

            InitializeSaveService();
            LoadCurrencies();

            OnInitialized?.Invoke();
        }
        private void InitializeSaveService()
        {
            saveService = new PlayerPrefsCurrencySaveService();
        }

        private void LoadCurrencies()
        {
            var loadedCurrencies = saveService.LoadCurrencies();

            if (loadedCurrencies == null || loadedCurrencies.Count == 0)
            {
                InitializeDefaultCurrencies();
            }
            else
            {
                currencies = loadedCurrencies;

                foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
                {
                    if (!currencies.ContainsKey(type))
                    {
                        currencies[type] = 0;
                    }
                }
            }
        }

        private void InitializeDefaultCurrencies()
        {
            currencies = new Dictionary<CurrencyType, int>();
            foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
            {
                currencies[type] = 0;
            }
        }

        private void SaveCurrencies()
        {
            saveService.SaveCurrencies(currencies);
        }

        public int GetCurrency(CurrencyType type) 
        {
            if(currencies.TryGetValue(type, out int amount))
            {
                return amount;
            }
            return 0;
        }
        public void AddCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) return;

            currencies[type] += amount;
            onCurrencyChanged?.Invoke(type, currencies[type]);
            SaveCurrencies();
        }
        public bool SpendCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) return false;

            if (currencies[type] >= amount)
            {
                currencies[type] -= amount;
                onCurrencyChanged?.Invoke(type, currencies[type]);
                SaveCurrencies();
                return true;
            }
            return false;
        }

    }
}