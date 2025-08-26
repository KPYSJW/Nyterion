using System.Collections.Generic;
using UnityEngine;
using System;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Utils;
using Zenject;

namespace Nytherion.Core.Managers
{
    public enum CurrencyType { Gold = 0, Token = 1 }
    public class CurrencyManager : BaseManager
    {
        [Header("Currency Settings")]
        [SerializeField] private bool enableAutoSave = true;
        
        private Dictionary<CurrencyType, int> currencies = new();
        public event Action<CurrencyType, int> onCurrencyChanged;
        private SaveLoadManager saveLoadManager;

        [Inject]
        public void Construct(SaveLoadManager saveLoadManager)
        {
            this.saveLoadManager = saveLoadManager;
        }
        
        protected override void OnInitializeInternal()
        {
            // 모든 통화 타입을 0으로 초기화
            foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
            {
                if (!currencies.ContainsKey(type))
                {
                    currencies[type] = 0;
                }
            }
        }
        public override void PopulateSaveData(SaveData saveData)
        {
            SaveLoadHelper.SafePopulateDictionary(
                saveData,
                currencies,
                (data, keys) => { data.currencyTypes.Clear(); data.currencyTypes.AddRange(keys); },
                (data, values) => { data.currencyAmounts.Clear(); data.currencyAmounts.AddRange(values); },
                nameof(CurrencyManager)
            );
        }
        public override void LoadFromSaveData(SaveData saveData)
        {
            SaveLoadHelper.SafeLoadDictionary(
                saveData,
                data => data?.currencyTypes,
                data => data?.currencyAmounts,
                loadedCurrencies =>
                {
                    currencies = new Dictionary<CurrencyType, int>();
                    
                    // 로드된 데이터가 있으면 사용, 없으면 기본값으로 초기화
                    if (loadedCurrencies.Count > 0)
                    {
                        currencies = loadedCurrencies;
                    }
                    
                    // 누락된 통화 타입들을 0으로 초기화
                    foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
                    {
                        if (!currencies.ContainsKey(type))
                        {
                            currencies[type] = 0;
                        }
                    }

                    // 모든 통화에 대해 변경 이벤트 발생
                    foreach (var currencyPair in currencies)
                    {
                        onCurrencyChanged?.Invoke(currencyPair.Key, currencyPair.Value);
                    }
                },
                nameof(CurrencyManager)
            );
        }
        public int GetCurrency(CurrencyType type)
        {
            return currencies.TryGetValue(type, out int amount) ? amount : 0;
        }
        public void AddCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) 
            {
                Debug.LogWarning($"[CurrencyManager] Invalid amount to add: {amount}. Amount must be positive.");
                return;
            }

            int newAmount = GetCurrency(type) + amount;
            currencies[type] = newAmount;
            onCurrencyChanged?.Invoke(type, newAmount);
            
            if (enableAutoSave && saveLoadManager != null)
            {
                saveLoadManager.SaveGame();
            }
            
            Debug.Log($"[CurrencyManager] Added {amount} {type}. New total: {newAmount}");
        }
        
        public bool SpendCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) 
            {
                Debug.LogWarning($"[CurrencyManager] Invalid amount to spend: {amount}. Amount must be positive.");
                return false;
            }

            int currentAmount = GetCurrency(type);
            if (currentAmount >= amount)
            {
                int newAmount = currentAmount - amount;
                currencies[type] = newAmount;
                onCurrencyChanged?.Invoke(type, newAmount);
                
                if (enableAutoSave && saveLoadManager != null)
                {
                    saveLoadManager.SaveGame();
                }
                
                Debug.Log($"[CurrencyManager] Spent {amount} {type}. Remaining: {newAmount}");
                return true;
            }
            
            Debug.LogWarning($"[CurrencyManager] Insufficient {type}. Required: {amount}, Available: {currentAmount}");
            return false;
        }

        // 재화가 충분한지 확인 
        public bool HasEnoughCurrency(CurrencyType type, int amount)
        {
            return GetCurrency(type) >= amount;
        }

        public override string GetStatusInfo()
        {
            var currencyInfo = "";
            foreach (var currency in currencies)
            {
                currencyInfo += $"{currency.Key}: {currency.Value}, ";
            }
            return $"{base.GetStatusInfo()}, Currencies: [{currencyInfo.TrimEnd(',', ' ')}]";
        }

    }
}