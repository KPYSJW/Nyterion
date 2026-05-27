using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Enums;
using VContainer;
using Nytherion.GamePlay.Relics;

namespace Nytherion.Core.Managers
{
    /// <summary>
    /// 순수 통화 데이터 관리 매니저
    /// UI 관련 기능 없이 데이터 로직만 담당
    /// </summary>
    public class CurrencyDataManager : BaseManager, IDataManager, ICurrencyDataNotifier
    {
        [Header("Currency Settings")]
        [SerializeField] private int initialGold = 1000;
        [SerializeField] private int initialTokens = 10;

        private Dictionary<CurrencyType, int> currencies;

        private SaveLoadManager saveLoadManager;

        private bool isLoadingFromSave = false;

        public event Action<CurrencyChangeData> OnDataChanged;

        protected override void Awake()
        {
            base.Awake();
        }

        [Inject]
        public void Construct(SaveLoadManager saveLoadManager)
        {
            this.saveLoadManager = saveLoadManager;
        }

        protected override void OnInitializeInternal()
        {
            InitializeCurrencies();
        }

        private void InitializeCurrencies()
        {
            currencies = new Dictionary<CurrencyType, int>
            {
                { CurrencyType.Gold, initialGold },
                { CurrencyType.Token, initialTokens }
            };
        }

        public bool AddCurrency(CurrencyType currencyType, int amount)
        {
            if (!IsInitialized || amount <= 0) return false;

            // 구멍 난 주머니 (TornPouch) 유물 효과 적용: 골드 획득 시 10% 확률로 획득량 2배
            if (currencyType == CurrencyType.Gold && !isLoadingFromSave)
            {
                RelicManager relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
                if (relicManager != null)
                {
                    foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                    {
                        RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                        if (block != null && block.RelicId == "TornPouch" && !block.SourceData.isDisabled)
                        {
                            if (UnityEngine.Random.Range(0, 100) < 10)
                            {
                                amount *= 2;
                                Debug.Log("[CurrencyDataManager] 구멍 난 주머니(TornPouch) 효과 발동! 획득 골드 2배!");
                            }
                            break;
                        }
                    }
                }
            }

            int oldAmount = GetCurrency(currencyType);
            int newAmount = oldAmount + amount;

            currencies[currencyType] = newAmount;

            NotifyDataChanged(new CurrencyChangeData
            {
                currencyType = currencyType,
                oldAmount = oldAmount,
                newAmount = newAmount,
                changeAmount = amount,
                isSilent = isLoadingFromSave
            });

            if (saveLoadManager != null && !isLoadingFromSave)
            {
                saveLoadManager.SaveGame();
            }
            return true;
        }

        public bool SpendCurrency(CurrencyType currencyType, int amount)
        {
            if (!IsInitialized || amount <= 0) return false;

            int oldAmount = GetCurrency(currencyType);

            if (oldAmount < amount)
            {
                Debug.LogWarning($"[CurrencyDataManager] {currencyType} 부족: 필요 {amount}, 보유 {oldAmount}");
                return false;
            }

            int newAmount = oldAmount - amount;
            currencies[currencyType] = newAmount;

            NotifyDataChanged(new CurrencyChangeData
            {
                currencyType = currencyType,
                oldAmount = oldAmount,
                newAmount = newAmount,
                changeAmount = -amount,
                isSilent = isLoadingFromSave
            });

            if (saveLoadManager != null && !isLoadingFromSave)
            {
                saveLoadManager.SaveGame();
            }
            return true;
        }

        public bool SetCurrency(CurrencyType currencyType, int amount)
        {
            if (!IsInitialized || amount < 0) return false;

            int oldAmount = GetCurrency(currencyType);
            currencies[currencyType] = amount;

            NotifyDataChanged(new CurrencyChangeData
            {
                currencyType = currencyType,
                oldAmount = oldAmount,
                newAmount = amount,
                changeAmount = amount - oldAmount,
                isSilent = isLoadingFromSave
            });

            if (saveLoadManager != null && !isLoadingFromSave)
            {
                saveLoadManager.SaveGame();
            }
            return true;
        }

        public int GetCurrency(CurrencyType currencyType)
        {
            if (!IsInitialized || !currencies.ContainsKey(currencyType))
                return 0;

            return currencies[currencyType];
        }

        public bool HasCurrency(CurrencyType currencyType, int requiredAmount)
        {
            return IsInitialized && GetCurrency(currencyType) >= requiredAmount;
        }

        public Dictionary<CurrencyType, int> GetAllCurrencies()
        {
            return IsInitialized ? new Dictionary<CurrencyType, int>(currencies) : new Dictionary<CurrencyType, int>();
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            if (!IsInitialized) return;

            saveData.currencyTypes.Clear();
            saveData.currencyAmounts.Clear();

            if (GameManager.IsVerboseLogging()) Debug.Log($"[CurrencyDataManager] 저장할 통화 데이터: {currencies.Count}개 항목");

            foreach (var kvp in currencies)
            {
                saveData.currencyTypes.Add(kvp.Key);
                saveData.currencyAmounts.Add(kvp.Value);

                if (GameManager.IsVerboseLogging()) Debug.Log($"[CurrencyDataManager] 저장: {kvp.Key} = {kvp.Value}");
            }
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            isLoadingFromSave = true;

            try
            {
                if (saveData == null || saveData.currencyTypes == null || saveData.currencyAmounts == null || saveData.currencyTypes.Count == 0)
                {
                    if (GameManager.IsVerboseLogging()) Debug.Log("[CurrencyDataManager] 저장 데이터가 없어 초기값으로 설정");
                    InitializeCurrencies();
                }
                else
                {
                    if (GameManager.IsVerboseLogging()) Debug.Log($"[CurrencyDataManager] 저장된 통화 데이터 로드: {saveData.currencyTypes.Count}개 항목");

                    currencies = new Dictionary<CurrencyType, int>();

                    for (int i = 0; i < saveData.currencyTypes.Count && i < saveData.currencyAmounts.Count; i++)
                    {
                        var currencyType = saveData.currencyTypes[i];
                        var amount = saveData.currencyAmounts[i];
                        currencies[currencyType] = amount;

                        if (GameManager.IsVerboseLogging()) Debug.Log($"[CurrencyDataManager] 로드: {currencyType} = {amount}");
                    }

                    if (!currencies.ContainsKey(CurrencyType.Gold))
                    {
                        currencies[CurrencyType.Gold] = initialGold;
                        if (GameManager.IsVerboseLogging()) Debug.Log($"[CurrencyDataManager] 골드 키가 없어 초기값 설정: {initialGold}");
                    }

                    if (!currencies.ContainsKey(CurrencyType.Token))
                    {
                        currencies[CurrencyType.Token] = initialTokens;
                        if (GameManager.IsVerboseLogging()) Debug.Log($"[CurrencyDataManager] 토큰 키가 없어 초기값 설정: {initialTokens}");
                    }
                }

                foreach (var kvp in currencies)
                {
                    NotifyDataChanged(new CurrencyChangeData
                    {
                        currencyType = kvp.Key,
                        oldAmount = 0,
                        newAmount = kvp.Value,
                        changeAmount = kvp.Value,
                        isSilent = true // 로딩 시에는 항상 silent
                    });
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CurrencyDataManager] 로드 중 오류 발생: {e.Message}");
                InitializeCurrencies();
            }
            finally
            {
                isLoadingFromSave = false;
            }
        }

        public void AddGold(int amount) => AddCurrency(CurrencyType.Gold, amount);
        public void AddTokens(int amount) => AddCurrency(CurrencyType.Token, amount);
        public int GetGold() => GetCurrency(CurrencyType.Gold);
        public int GetTokens() => GetCurrency(CurrencyType.Token);

        /// <summary>
        /// 게임 오버(사망) 초기화 시 기본 재화(Gold)를 0으로 초기화
        /// 영구 재화(Token)는 초기화되지 않는다
        /// </summary>
        public void ResetBasicCurrencies()
        {
            if (!IsInitialized) return;

            SetCurrency(CurrencyType.Gold, 0);

            if (GameManager.IsVerboseLogging())
                Debug.Log("[CurrencyDataManager] 사망/재시작으로 인해 기본 재화(Gold)가 0으로 초기화되었습니다.");
        }

        private void NotifyDataChanged(CurrencyChangeData changeData)
        {
            OnDataChanged?.Invoke(changeData);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}