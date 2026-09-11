using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 골드 획득량을 높이고 짧은 시간 안에 연속 획득하면 황금 열기를 누적한다.
    /// </summary>
    [Serializable, RelicDisplayName("황금 사냥꾼 세트 강화")]
    public sealed class GoldAcquisitionSetBonusEffect : RelicEffectBase
    {
        [Min(0f)] public float baseGoldGainBonus;
        [Range(0f, 1f)] public float bonusGoldOnKillChance;
        [Min(0)] public int bonusGoldOnKillAmount;

        [Header("황금 열기")]
        [Min(0f)] public float feverBonusPerStack;
        [Min(0)] public int maxFeverStacks;
        [Min(0.1f)] public float feverWindow = 3f;
        [Range(0f, 1f)] public float jackpotChanceAtMaxStacks;
        [Min(0)] public int jackpotGoldAmount;

        [NonSerialized] private GoldAcquisitionSetBonusRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<GoldAcquisitionSetBonusRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<GoldAcquisitionSetBonusRuntime>();
            }

            appliedRuntime.SetBonus(this, playerManager, new GoldAcquisitionSetBonusValues
            {
                BaseGoldGainBonus = baseGoldGainBonus,
                BonusGoldOnKillChance = bonusGoldOnKillChance,
                BonusGoldOnKillAmount = bonusGoldOnKillAmount,
                FeverBonusPerStack = feverBonusPerStack,
                MaxFeverStacks = maxFeverStacks,
                FeverWindow = feverWindow,
                JackpotChanceAtMaxStacks = jackpotChanceAtMaxStacks,
                JackpotGoldAmount = jackpotGoldAmount
            });
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<GoldAcquisitionSetBonusRuntime>();
            }

            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public struct GoldAcquisitionSetBonusValues
    {
        public float BaseGoldGainBonus;
        public float BonusGoldOnKillChance;
        public int BonusGoldOnKillAmount;
        public float FeverBonusPerStack;
        public int MaxFeverStacks;
        public float FeverWindow;
        public float JackpotChanceAtMaxStacks;
        public int JackpotGoldAmount;
    }

    public sealed class GoldAcquisitionSetBonusRuntime : MonoBehaviour
    {
        private readonly Dictionary<GoldAcquisitionSetBonusEffect, GoldAcquisitionSetBonusValues> bonuses =
            new Dictionary<GoldAcquisitionSetBonusEffect, GoldAcquisitionSetBonusValues>();

        private CurrencyDataManager currencyDataManager;
        private EventManager eventManager;
        private bool isGrantingBonus;
        private float lastGoldGainTime = float.NegativeInfinity;

        public float BaseGoldGainBonus { get; private set; }
        public float FeverBonusPerStack { get; private set; }
        public int MaxFeverStacks { get; private set; }
        public float FeverWindow { get; private set; } = 3f;
        public int CurrentFeverStacks { get; private set; }
        private float BonusGoldOnKillChance { get; set; }
        private int BonusGoldOnKillAmount { get; set; }
        private float JackpotChanceAtMaxStacks { get; set; }
        private int JackpotGoldAmount { get; set; }

        public void SetBonus(
            GoldAcquisitionSetBonusEffect source,
            PlayerManager playerManager,
            GoldAcquisitionSetBonusValues values)
        {
            if (source == null || playerManager == null) return;

            bonuses[source] = values;
            ConfigureDependencies(playerManager);
            Recalculate();
        }

        public void RemoveBonus(GoldAcquisitionSetBonusEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        private void ConfigureDependencies(PlayerManager playerManager)
        {
            CurrencyDataManager newCurrencyManager = DataLifetimeScope.Instance != null
                ? DataLifetimeScope.Instance.GetDataManager<CurrencyDataManager>()
                : null;
            if (currencyDataManager != newCurrencyManager)
            {
                if (currencyDataManager != null)
                {
                    currencyDataManager.OnDataChanged -= HandleCurrencyChanged;
                }

                currencyDataManager = newCurrencyManager;
                if (currencyDataManager != null)
                {
                    currencyDataManager.OnDataChanged += HandleCurrencyChanged;
                }
            }

            EventManager newEventManager = playerManager.EventManager;
            if (eventManager != newEventManager)
            {
                if (eventManager != null)
                {
                    eventManager.OnEnemyDied -= HandleEnemyDied;
                }

                eventManager = newEventManager;
                if (eventManager != null)
                {
                    eventManager.OnEnemyDied += HandleEnemyDied;
                }
            }
        }

        private void Recalculate()
        {
            BaseGoldGainBonus = 0f;
            BonusGoldOnKillChance = 0f;
            BonusGoldOnKillAmount = 0;
            FeverBonusPerStack = 0f;
            MaxFeverStacks = 0;
            FeverWindow = 3f;
            JackpotChanceAtMaxStacks = 0f;
            JackpotGoldAmount = 0;

            foreach (GoldAcquisitionSetBonusValues bonus in bonuses.Values)
            {
                BaseGoldGainBonus = Mathf.Max(BaseGoldGainBonus, bonus.BaseGoldGainBonus);
                BonusGoldOnKillChance = Mathf.Max(BonusGoldOnKillChance, bonus.BonusGoldOnKillChance);
                BonusGoldOnKillAmount = Mathf.Max(BonusGoldOnKillAmount, bonus.BonusGoldOnKillAmount);
                FeverBonusPerStack = Mathf.Max(FeverBonusPerStack, bonus.FeverBonusPerStack);
                MaxFeverStacks = Mathf.Max(MaxFeverStacks, bonus.MaxFeverStacks);
                FeverWindow = Mathf.Max(FeverWindow, bonus.FeverWindow);
                JackpotChanceAtMaxStacks = Mathf.Max(
                    JackpotChanceAtMaxStacks,
                    bonus.JackpotChanceAtMaxStacks);
                JackpotGoldAmount = Mathf.Max(JackpotGoldAmount, bonus.JackpotGoldAmount);
            }

            if (bonuses.Count == 0)
            {
                CurrentFeverStacks = 0;
            }
        }

        private void HandleCurrencyChanged(CurrencyChangeData data)
        {
            if (isGrantingBonus || bonuses.Count == 0 || data.isSilent ||
                data.currencyType != CurrencyType.Gold || data.changeAmount <= 0)
            {
                return;
            }

            if (MaxFeverStacks > 0)
            {
                CurrentFeverStacks = Time.time - lastGoldGainTime <= FeverWindow
                    ? Mathf.Min(MaxFeverStacks, CurrentFeverStacks + 1)
                    : 1;
                lastGoldGainTime = Time.time;
            }

            float bonusRatio = BaseGoldGainBonus + FeverBonusPerStack * CurrentFeverStacks;
            GrantBonusGold(Mathf.RoundToInt(data.changeAmount * Mathf.Max(0f, bonusRatio)));
        }

        private void HandleEnemyDied(EnemyBase _)
        {
            if (bonuses.Count == 0) return;

            if (BonusGoldOnKillAmount > 0 && UnityEngine.Random.value < BonusGoldOnKillChance)
            {
                GrantBonusGold(BonusGoldOnKillAmount);
            }

            if (MaxFeverStacks > 0 && CurrentFeverStacks >= MaxFeverStacks &&
                JackpotGoldAmount > 0 && UnityEngine.Random.value < JackpotChanceAtMaxStacks)
            {
                GrantBonusGold(JackpotGoldAmount);
            }
        }

        public void GrantBonusGold(int amount)
        {
            if (amount <= 0 || currencyDataManager == null) return;

            try
            {
                isGrantingBonus = true;
                currencyDataManager.AddUnmodifiedCurrency(CurrencyType.Gold, amount);
            }
            finally
            {
                isGrantingBonus = false;
            }
        }

        private void OnDestroy()
        {
            if (currencyDataManager != null)
            {
                currencyDataManager.OnDataChanged -= HandleCurrencyChanged;
            }
            if (eventManager != null)
            {
                eventManager.OnEnemyDied -= HandleEnemyDied;
            }
        }
    }
}
