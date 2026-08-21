using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Core.Managers;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Utils;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Nytherion.UI.Inventory
{
    public class CharacterStatsUI : MonoBehaviour, IInitializable
    {
        [Header("References")]
        [SerializeField] private RectTransform statsContainer;
        [SerializeField] private GameObject statCellPrefab;


        private readonly List<GameObject> statCells = new List<GameObject>();
        private PlayerManager playerManager;
        private bool isLocalizationSubscribed;

        [Inject]
        public void Construct(PlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }
        public void Initialize()
        {
            RefreshStatsUI();
        }

        private void Start()
        {
            // Initialize()로 이동했으므로 비워둠
        }
        private void OnEnable()
        {
            LocalizationText.LanguageChanged += OnTemporaryLanguageChanged;

            if (LocalizationText.IsConfigured)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
                isLocalizationSubscribed = true;
            }

            if (playerManager != null)
            {
                playerManager.OnPlayerStatsChanged -= RefreshStatsUI;
                playerManager.OnPlayerStatsChanged += RefreshStatsUI;
                RefreshStatsUI();
            }
        }

        private void OnDisable()
        {
            LocalizationText.LanguageChanged -= OnTemporaryLanguageChanged;

            if (isLocalizationSubscribed)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
                isLocalizationSubscribed = false;
            }

            if (playerManager != null)
            {
                playerManager.OnPlayerStatsChanged -= RefreshStatsUI;
            }
        }

        private void OnTemporaryLanguageChanged()
        {
            RefreshStatsUI();
        }

        private bool ValidateReferences()
        {
            if (playerManager == null)
            {
                Debug.LogError("PlayerManager를 찾을 수 없습니다.", this);
                return false;
            }
            if (statsContainer == null)
            {
                Debug.LogError("Stats Container가 할당되지 않았습니다.", this);
                return false;
            }
            if (statCellPrefab == null)
            {
                Debug.LogError("Stat Cell Prefab이 할당되지 않았습니다.", this);
                return false;
            }

            return true;
        }

        public void RefreshStatsUI()
        {
            if (!ValidateReferences()) return;

            ClearStatsUI();
            CreateStatCells();

        }

        private void CreateStatCells()
        {
            PlayerData currentPlayerData = playerManager.currentPlayerData;
            if (currentPlayerData == null) return;

            System.Reflection.FieldInfo[] fields = typeof(PlayerData).GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                if (field.Name == "dashSpeed" || field.Name == "dashDuration" || field.Name == "dashCooldown" || field.Name == "dashDistance") continue;

                object value = field.GetValue(currentPlayerData);
                if (value == null) continue;

                string displayValue = value.ToString();
                if (value is float floatValue)
                {
                    if (field.Name == "critChance" || field.Name == "lifesteal" || field.Name == "chargeTimeReduction" || field.Name == "critDamageMultiplier")
                    {
                        displayValue = $"{Mathf.RoundToInt(floatValue * 100f)}%";
                    }
                    else if (field.Name == "meleeSpeed" || field.Name == "rangedSpeed")
                    {
                        displayValue = $"{floatValue:0.##}";
                    }
                    else
                    {
                        displayValue = Mathf.RoundToInt(floatValue).ToString();
                    }
                }

                GameObject cell = CreateStatCell(field.Name, displayValue);
                if (cell != null)
                {
                    statCells.Add(cell);
                }
            }
        }

        private GameObject CreateStatCell(string statName, string value)
        {
            if (statCellPrefab == null || statsContainer == null)
                return null;

            try
            {
                var cell = Instantiate(statCellPrefab, statsContainer);
                var text = cell.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = $"{GetLocalizedStatName(statName)}: {value}";
                }
                return cell;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"스탯 셀 생성 오류: {e.Message}");
                return null;
            }
        }

        private void ClearStatsUI()
        {
            foreach (var cell in statCells)
            {
                if (cell != null)
                {
                    Destroy(cell);
                }
            }
            statCells.Clear();
        }



        private string GetLocalizedStatName(string statName)
        {
            (string korean, string english) = statName switch
            {
                "maxHealth" => ("최대 체력", "Max Health"),
                "defense" => ("방어력", "Defense"),
                "moveSpeed" => ("이동 속도", "Move Speed"),
                "meleeDamage" => ("근접 공격력", "Melee Damage"),
                "rangedDamage" => ("원거리 공격력", "Ranged Damage"),
                "meleeSpeed" => ("근접 공격 속도", "Melee Attack Speed"),
                "rangedSpeed" => ("원거리 공격 속도", "Ranged Attack Speed"),
                "extraProjectiles" => ("추가 투사체 수", "Extra Projectiles"),
                "lifesteal" => ("생명력 흡수", "Lifesteal"),
                "chargeTimeReduction" => ("충전 시간 감소", "Charge Time Reduction"),
                "critChance" => ("치명타 확률", "Critical Chance"),
                "critDamageMultiplier" => ("치명타 피해량", "Critical Damage"),
                _ => (statName, statName)
            };

            return LocalizationText.Get(
                LocalizationTables.UI,
                $"ui.stat.{statName}",
                korean,
                english);
        }

        private void OnLocaleChanged(Locale _)
        {
            RefreshStatsUI();
        }
    }
}
