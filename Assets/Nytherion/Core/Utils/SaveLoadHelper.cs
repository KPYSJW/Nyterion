using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Data;

namespace Nytherion.Core.Utils
{
    /// <summary>
    /// 저장/로딩 작업에서 공통으로 사용되는 유틸리티 메서드들을 제공합니다.
    /// 중복된 null 체크, 예외 처리, 데이터 변환 로직을 표준화합니다.
    /// </summary>
    public static class SaveLoadHelper
    {
        /// <summary>
        /// 안전하게 데이터를 저장합니다. null 체크와 예외 처리를 포함합니다.
        /// </summary>
        /// <typeparam name="T">저장할 데이터 타입</typeparam>
        /// <param name="saveData">저장 데이터 객체</param>
        /// <param name="data">저장할 데이터</param>
        /// <param name="setter">데이터를 SaveData에 설정하는 액션</param>
        /// <param name="managerName">디버깅을 위한 매니저 이름</param>
        /// <returns>저장 성공 여부</returns>
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
                Debug.Log($"[{managerName}] Data saved successfully.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{managerName}] Failed to save data: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 안전하게 데이터를 로딩합니다. null 체크, 기본값 설정, 예외 처리를 포함합니다.
        /// </summary>
        /// <typeparam name="T">로딩할 데이터 타입</typeparam>
        /// <param name="saveData">저장 데이터 객체</param>
        /// <param name="getter">SaveData에서 데이터를 가져오는 함수</param>
        /// <param name="applier">가져온 데이터를 적용하는 액션</param>
        /// <param name="defaultValue">데이터가 없을 때 사용할 기본값</param>
        /// <param name="managerName">디버깅을 위한 매니저 이름</param>
        /// <returns>로딩 성공 여부</returns>
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
                Debug.Log($"[{managerName}] Data loaded successfully.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{managerName}] Failed to load data: {e.Message}. Using default value.");
                applier(defaultValue);
                return false;
            }
        }

        /// <summary>
        /// 컬렉션 데이터를 안전하게 저장합니다.
        /// </summary>
        /// <typeparam name="T">컬렉션 요소 타입</typeparam>
        /// <param name="saveData">저장 데이터 객체</param>
        /// <param name="collection">저장할 컬렉션</param>
        /// <param name="setter">컬렉션을 SaveData에 설정하는 액션</param>
        /// <param name="managerName">디버깅을 위한 매니저 이름</param>
        /// <returns>저장 성공 여부</returns>
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

        /// <summary>
        /// 컬렉션 데이터를 안전하게 로딩합니다.
        /// </summary>
        /// <typeparam name="T">컬렉션 요소 타입</typeparam>
        /// <param name="saveData">저장 데이터 객체</param>
        /// <param name="getter">SaveData에서 컬렉션을 가져오는 함수</param>
        /// <param name="applier">가져온 컬렉션을 적용하는 액션</param>
        /// <param name="managerName">디버깅을 위한 매니저 이름</param>
        /// <returns>로딩 성공 여부</returns>
        public static bool SafeLoadCollection<T>(SaveData saveData, Func<SaveData, List<T>> getter, Action<List<T>> applier, string managerName = "Unknown")
        {
            return SafeLoadData(saveData, getter, applier, new List<T>(), managerName);
        }

        /// <summary>
        /// Dictionary를 두 개의 리스트로 변환하여 저장합니다.
        /// </summary>
        /// <typeparam name="TKey">딕셔너리 키 타입</typeparam>
        /// <typeparam name="TValue">딕셔너리 값 타입</typeparam>
        /// <param name="saveData">저장 데이터 객체</param>
        /// <param name="dictionary">저장할 딕셔너리</param>
        /// <param name="keySetter">키 리스트를 SaveData에 설정하는 액션</param>
        /// <param name="valueSetter">값 리스트를 SaveData에 설정하는 액션</param>
        /// <param name="managerName">디버깅을 위한 매니저 이름</param>
        /// <returns>저장 성공 여부</returns>
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

        /// <summary>
        /// 두 개의 리스트로부터 Dictionary를 복원합니다.
        /// </summary>
        /// <typeparam name="TKey">딕셔너리 키 타입</typeparam>
        /// <typeparam name="TValue">딕셔너리 값 타입</typeparam>
        /// <param name="saveData">저장 데이터 객체</param>
        /// <param name="keyGetter">SaveData에서 키 리스트를 가져오는 함수</param>
        /// <param name="valueGetter">SaveData에서 값 리스트를 가져오는 함수</param>
        /// <param name="applier">복원된 딕셔너리를 적용하는 액션</param>
        /// <param name="managerName">디버깅을 위한 매니저 이름</param>
        /// <returns>로딩 성공 여부</returns>
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
                Debug.Log($"[{managerName}] Dictionary loaded successfully with {dictionary.Count} entries.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{managerName}] Failed to load dictionary: {e.Message}");
                applier(new Dictionary<TKey, TValue>());
                return false;
            }
        }

        /// <summary>
        /// 데이터 유효성을 검사합니다.
        /// </summary>
        /// <typeparam name="T">검사할 데이터 타입</typeparam>
        /// <param name="data">검사할 데이터</param>
        /// <param name="validator">유효성 검사 함수</param>
        /// <param name="defaultValue">유효하지 않을 때 사용할 기본값</param>
        /// <param name="managerName">디버깅을 위한 매니저 이름</param>
        /// <returns>유효한 데이터</returns>
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