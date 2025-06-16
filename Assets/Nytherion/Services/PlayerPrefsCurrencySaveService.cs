using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Core;

namespace Nytherion.Services
{
    public class PlayerPrefsCurrencySaveService : ICurrencySaveService
    {
        private const string SAVE_KEY = "PlayerCurrencies";
        private const string VERSION_KEY = "_CurrencyVersion";
        private const int CURRENT_VERSION = 1;

        [Serializable]
        private class CurrencySaveData
        {
            [Serializable]
            public class CurrencyEntry
            {
                public CurrencyType type;
                public int amount;
            }

            public List<CurrencyEntry> currencies = new();
            public int version;
            
            public Dictionary<CurrencyType, int> ToDictionary()
            {
                var dict = new Dictionary<CurrencyType, int>();
                foreach (var entry in currencies)
                {
                    dict[entry.type] = entry.amount;
                }
                return dict;
            }
            
            public static CurrencySaveData FromDictionary(Dictionary<CurrencyType, int> dict, int version)
            {
                var data = new CurrencySaveData { version = version };
                foreach (var kvp in dict)
                {
                    data.currencies.Add(new CurrencyEntry { type = kvp.Key, amount = kvp.Value });
                }
                return data;
            }
        }

        void ICurrencySaveService.SaveCurrencies(Dictionary<CurrencyType, int> currencies)
        {
            try
            {
var saveData = CurrencySaveData.FromDictionary(currencies, CURRENT_VERSION);
                string json = JsonUtility.ToJson(saveData);
                PlayerPrefs.SetString(SAVE_KEY, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"재화 저장 중 오류 발생: {e.Message}");
            }
        }

        Dictionary<CurrencyType, int> ICurrencySaveService.LoadCurrencies()
        {
            try
            {
                if (!PlayerPrefs.HasKey(SAVE_KEY))
                {
                    return InitializeDefaultCurrencies();
                }

                string json = PlayerPrefs.GetString(SAVE_KEY);
                var saveData = JsonUtility.FromJson<CurrencySaveData>(json);

                // 버전이 다른 경우 마이그레이션 로직을 추가할 수 있습니다.
                if (saveData.version != CURRENT_VERSION)
                {
                    // 버전 업데이트 로직 (필요시)
                }


                return saveData.ToDictionary();
            }
            catch (Exception e)
            {
                Debug.LogError($"재화 불러오기 중 오류 발생: {e.Message}");
                return InitializeDefaultCurrencies();
            }
        }

        private Dictionary<CurrencyType, int> InitializeDefaultCurrencies()
        {
            var defaultCurrencies = new Dictionary<CurrencyType, int>();
            foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
            {
                defaultCurrencies[type] = 0;
            }
            return defaultCurrencies;
        }
    }
}
