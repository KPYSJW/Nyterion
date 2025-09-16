using System.Collections.Generic;
using UnityEngine;
using System;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Utils;
using VContainer;
using VContainer.Unity;
using TMPro;

namespace Nytherion.Core.Managers
{
    public enum CurrencyType { Gold = 0, Token = 1 }
    public class CurrencyManager : BaseManager
    {
        private Dictionary<CurrencyType, int> currencies = new();
        public event Action<CurrencyType, int> onCurrencyChanged;
        private SaveLoadManager saveLoadManager;
        private IObjectResolver container;
        private GameSceneUIRefs gameSceneUIRefs;
        private TMP_Text goldText;
        private TMP_Text tokenText;

        [Inject]
        public void Construct(IObjectResolver container,
            GameSceneUIRefs gameSceneUIRefs)
        {
            this.container = container;
            this.gameSceneUIRefs = gameSceneUIRefs;
            this.goldText = gameSceneUIRefs.CurrencyDisplays[0].AmountText;
            this.tokenText = gameSceneUIRefs.CurrencyDisplays[1].AmountText;
        }

        protected override void OnInitializeInternal()
        {
            if (saveLoadManager == null && container != null)
            {
                saveLoadManager = container.Resolve<SaveLoadManager>();
            }

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

                    if (loadedCurrencies.Count > 0)
                    {
                        currencies = loadedCurrencies;
                    }

                    foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
                    {
                        if (!currencies.ContainsKey(type))
                        {
                            currencies[type] = 0;
                        }
                    }

                    foreach (var currencyPair in currencies)
                    {
                        onCurrencyChanged?.Invoke(currencyPair.Key, currencyPair.Value);
                    }
                },
                nameof(CurrencyManager)
            );
            UpdateCurrency(CurrencyType.Gold, currencies[CurrencyType.Gold], "");
            UpdateCurrency(CurrencyType.Token, currencies[CurrencyType.Token], "");
        }
        public int GetCurrency(CurrencyType type)
        {
            return currencies.TryGetValue(type, out int amount) ? amount : 0;
        }
        public void AddCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) 
            {
                return;
            }

            UpdateCurrency(type, GetCurrency(type) + amount, $"Added {amount} {type}.");
        }
        
        public bool SpendCurrency(CurrencyType type, int amount)
        {
            if (amount <= 0) 
            {
                return false;
            }

            int currentAmount = GetCurrency(type);
            if (currentAmount >= amount)
            {
                UpdateCurrency(type, currentAmount - amount, $"Spent {amount} {type}.");
                return true;
            }
            
            return false;
        }

        // 재화가 충분한지 확인 
        public bool HasEnoughCurrency(CurrencyType type, int amount)
        {
            return GetCurrency(type) >= amount;
        }

        private void UpdateCurrency(CurrencyType type, int newAmount, string logMessage)
        {
            currencies[type] = newAmount;
            onCurrencyChanged?.Invoke(type, newAmount);

            if (type == CurrencyType.Gold)
            {
                goldText.text = newAmount.ToString();
            }
            else if (type == CurrencyType.Token)
            {
                tokenText.text = newAmount.ToString();
            }

            // 자동 저장은 너무 빈번하므로 주석 처리 - 게임 종료시에만 저장
            // if (enableAutoSave && GetSaveLoadManager() != null)
            // {
            //     GetSaveLoadManager().SaveGame();
            // }

        }
        
        private SaveLoadManager GetSaveLoadManager()
        {
            if (saveLoadManager == null && container != null)
            {
                try
                {
                    saveLoadManager = container.Resolve<SaveLoadManager>();
                }
                catch (VContainerException)
                {
                    // SaveLoadManager가 아직 해결되지 않은 경우 null을 반환
                    return null;
                }
            }
            return saveLoadManager;
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