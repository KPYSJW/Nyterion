using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Core.Managers;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Inventory
{
    public class CharacterStatsUI : MonoBehaviour, IInitializable
    {
        [Header("References")]
        [SerializeField] private RectTransform statsContainer;
        [SerializeField] private GameObject statCellPrefab;


        private readonly List<GameObject> statCells = new List<GameObject>();
        private PlayerManager playerManager;

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
            if (playerManager != null)
            {
                playerManager.OnPlayerStatsChanged -= RefreshStatsUI;
                playerManager.OnPlayerStatsChanged += RefreshStatsUI;
                RefreshStatsUI();
            }
        }

        private void OnDisable()
        {
            if (playerManager != null)
            {
                playerManager.OnPlayerStatsChanged -= RefreshStatsUI;
            }
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
                    text.text = $"{GetKoreanStatName(statName)}: {value}";
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



        private string GetKoreanStatName(string englishName)
        {
            return englishName switch
            {
                "maxHealth" => "최대 체력",
                "defense" => "방어력",
                "moveSpeed" => "이동 속도",
                "meleeDamage" => "근접 공격력",
                "rangedDamage" => "원거리 공격력",
                "meleeSpeed" => "근접 공격 속도",
                "rangedSpeed" => "원거리 공격 속도",
                "extraProjectiles" => "추가 투사체 수",
                "lifesteal" => "생명력 흡수",
                "chargeTimeReduction" => "충전 시간 감소",
                "critChance" => "치명타 확률",
                "critDamageMultiplier" => "치명타 피해량",
                _ => englishName
            };
        }
    }
}