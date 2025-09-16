using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Data;

namespace Nytherion.Core.Utils
{
    
    public static class SaveLoadHelper
    {
        
        public static bool SafePopulateData<T>(SaveData saveData, T data, Action<SaveData, T> setter, string managerName = "Unknown")
        {
            if (saveData == null)
            {
                Debug.LogError($"[{managerName}] SaveData is null. Cannot save data.");
                return false;
            }

            if (setter == null)
            {
                Debug.LogError($"[{managerName}] Setter action is null. Cannot save data.");
                return false;
            }

            try
            {
                setter(saveData, data);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{managerName}] Failed to save data: {e.Message}");
                return false;
            }
        }

        public static bool SafeLoadData<T>(SaveData saveData, Func<SaveData, T> getter, Action<T> applier, T defaultValue = default, string managerName = "Unknown")
        {
            if (applier == null)
            {
                Debug.LogError($"[{managerName}] Applier action is null. Cannot load data.");
                return false;
            }

            if (saveData == null)
            {
                Debug.LogWarning($"[{managerName}] SaveData is null. Using default value.");
                applier(defaultValue);
                return false;
            }

            if (getter == null)
            {
                Debug.LogError($"[{managerName}] Getter function is null. Using default value.");
                applier(defaultValue);
                return false;
            }

            try
            {
                T data = getter(saveData);
                applier(data != null ? data : defaultValue);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{managerName}] Failed to load data: {e.Message}. Using default value.");
                applier(defaultValue);
                return false;
            }
        }

        
        public static bool SafePopulateCollection<T>(SaveData saveData, IEnumerable<T> collection, Action<SaveData, List<T>> setter, string managerName = "Unknown")
        {
            if (collection == null)
            {
                return SafePopulateData(saveData, new List<T>(), setter, managerName);
            }

            try
            {
                var list = new List<T>(collection);
                return SafePopulateData(saveData, list, setter, managerName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{managerName}] Failed to convert collection to list: {e.Message}");
                return SafePopulateData(saveData, new List<T>(), setter, managerName);
            }
        }

        public static bool SafeLoadCollection<T>(SaveData saveData, Func<SaveData, List<T>> getter, Action<List<T>> applier, string managerName = "Unknown")
        {
            return SafeLoadData(saveData, getter, applier, new List<T>(), managerName);
        }

        public static bool SafePopulateDictionary<TKey, TValue>(SaveData saveData, Dictionary<TKey, TValue> dictionary, 
            Action<SaveData, List<TKey>> keySetter, Action<SaveData, List<TValue>> valueSetter, string managerName = "Unknown")
        {
            if (dictionary == null)
            {
                return SafePopulateData(saveData, new List<TKey>(), keySetter, managerName) &&
                       SafePopulateData(saveData, new List<TValue>(), valueSetter, managerName);
            }

            try
            {
                var keys = new List<TKey>(dictionary.Keys);
                var values = new List<TValue>(dictionary.Values);
                
                return SafePopulateData(saveData, keys, keySetter, managerName) &&
                       SafePopulateData(saveData, values, valueSetter, managerName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{managerName}] Failed to convert dictionary to lists: {e.Message}");
                return false;
            }
        }

        public static bool SafeLoadDictionary<TKey, TValue>(SaveData saveData, 
            Func<SaveData, List<TKey>> keyGetter, Func<SaveData, List<TValue>> valueGetter, 
            Action<Dictionary<TKey, TValue>> applier, string managerName = "Unknown")
        {
            if (applier == null)
            {
                Debug.LogError($"[{managerName}] Applier action is null. Cannot load dictionary.");
                return false;
            }

            try
            {
                var keys = keyGetter?.Invoke(saveData) ?? new List<TKey>();
                var values = valueGetter?.Invoke(saveData) ?? new List<TValue>();

                if (keys.Count != values.Count)
                {
                    Debug.LogWarning($"[{managerName}] Key count ({keys.Count}) doesn't match value count ({values.Count}). Using empty dictionary.");
                    applier(new Dictionary<TKey, TValue>());
                    return false;
                }

                var dictionary = new Dictionary<TKey, TValue>();
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] != null && !dictionary.ContainsKey(keys[i]))
                    {
                        dictionary[keys[i]] = values[i];
                    }
                }

                applier(dictionary);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{managerName}] Failed to load dictionary: {e.Message}");
                applier(new Dictionary<TKey, TValue>());
                return false;
            }
        }

        
        public static T ValidateData<T>(T data, Func<T, bool> validator, T defaultValue = default, string managerName = "Unknown")
        {
            if (validator == null)
            {
                Debug.LogWarning($"[{managerName}] Validator is null. Skipping validation.");
                return data;
            }

            try
            {
                if (validator(data))
                {
                    return data;
                }
                else
                {
                    Debug.LogWarning($"[{managerName}] Data validation failed. Using default value.");
                    return defaultValue;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[{managerName}] Error during data validation: {e.Message}. Using default value.");
                return defaultValue;
            }
        }
    }
}