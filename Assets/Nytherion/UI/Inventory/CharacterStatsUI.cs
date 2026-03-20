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
        [SerializeField] private ScrollRect scrollRect;

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
            if (scrollRect == null)
            {
                Debug.LogError("ScrollRect가 할당되지 않았습니다.", this);
                return false;
            }
            return true;
        }

        public void RefreshStatsUI()
        {
            if (!ValidateReferences()) return;

            ClearStatsUI();
            CreateStatCells();
            StartCoroutine(ResetScrollPosition());
        }

        private void CreateStatCells()
        {
            PlayerData currentPlayerData = playerManager.currentPlayerData;
            if (currentPlayerData == null) return;

            var fields = typeof(PlayerData).GetFields();
            foreach (var field in fields)
            {
                var value = field.GetValue(currentPlayerData);
                if (value == null) continue;

                var cell = CreateStatCell(field.Name, value.ToString());
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

        private IEnumerator ResetScrollPosition()
        {
            yield return new WaitForEndOfFrame();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
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
                "dashSpeed" => "대시 속도",
                "dashDuration" => "대시 지속시간",
                "dashCooldown" => "대시 쿨다운",
                _ => englishName
            };
        }
    }
}