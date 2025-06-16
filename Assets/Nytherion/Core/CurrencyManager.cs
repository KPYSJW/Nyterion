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
        private Dictionary<CurrencyType, int> currencies = new();
        public event Action<CurrencyType, int> onCurrencyChanged;
        
        private ICurrencySaveService saveService;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // 부모가 있다면 부모를 파괴하지 않도록 설정
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);
                InitializeSaveService();
                LoadCurrencies();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void InitializeSaveService()
        {
            saveService = new PlayerPrefsCurrencySaveService();
            // 또는 의존성 주입을 사용하는 경우: saveService = ServiceLocator.Current.Get<ICurrencySaveService>();
        }
        
        private void LoadCurrencies()
        {
            var loadedCurrencies = saveService.LoadCurrencies();
            
            // 저장된 데이터가 없거나 비어있는 경우 기본값으로 초기화
            if (loadedCurrencies == null || loadedCurrencies.Count == 0)
            {
                InitializeDefaultCurrencies();
            }
            else
            {
                // 저장된 데이터로 업데이트
                currencies = loadedCurrencies;
                
                // 모든 CurrencyType이 있는지 확인하고 없으면 추가
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

        public int GetCurrency(CurrencyType type) => currencies[type];
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