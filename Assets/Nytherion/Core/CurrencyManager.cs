using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Nytherion.Core
{
    public enum CurrencyType
    {
        Gold,
        Gem,
        Token
    }
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }
        private Dictionary<CurrencyType, int> currencies = new();
        public event Action<CurrencyType, int> onCurrencyChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
            {
                currencies[type] = 0;
            }
        }

        public int GetCurrency(CurrencyType type) => currencies[type];
        public void AddCurrency(CurrencyType type, int amount)
        {
            currencies[type] += amount;
            onCurrencyChanged?.Invoke(type, currencies[type]);
        }
        public bool SpendCurrency(CurrencyType type, int amount)
        {
            if (currencies[type] >= amount)
            {
                currencies[type] -= amount;
                onCurrencyChanged?.Invoke(type, currencies[type]);
                return true;
            }
            return false;
            
        }
     
    }
}