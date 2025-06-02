using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Player;

namespace Nytherion.UI.Inventory
{
    public class CharacterStatsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerData playerData;
        [SerializeField] private RectTransform statsContainer;
        [SerializeField] private GameObject statCellPrefab;
        [SerializeField] private ScrollRect scrollRect;

        private readonly List<GameObject> statCells = new List<GameObject>();

        private void Start()
        {
            if (!ValidateReferences()) 
            {
                Debug.LogError("필수 참조가 설정되지 않았습니다.", this);
                return;
            }
            
            ClearStatsUI();
            CreateStatCells();
            StartCoroutine(ResetScrollPosition());
        }

        private bool ValidateReferences()
        {
            if (playerData == null)
            {
                Debug.LogError("PlayerData가 할당되지 않았습니다.", this);
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

        private void CreateStatCells()
        {
            if (playerData == null) return;

            var fields = typeof(PlayerData).GetFields();
            foreach (var field in fields)
            {
                var value = field.GetValue(playerData);
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
                    if (Application.isPlaying)
                        Destroy(cell);
                    else
                        DestroyImmediate(cell);
                }
            }
            statCells.Clear();
        }

        private IEnumerator ResetScrollPosition()
        {
            yield return null; // 한 프레임 대기
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private string GetKoreanStatName(string englishName)
        {
            return englishName switch
            {
                "maxHealth" => "Health",
                "moveSpeed" => "Move Speed",
                "meleeDamage" => "Melee Damage",
                "rangedDamage" => "Ranged Damage",
                "meleeSpeed" => "Melee Speed",
                "rangedSpeed" => "Ranged Speed",
                "dashSpeed" => "Dash Speed",
                "dashDuration" => "Dash Duration",
                "dashCooldown" => "Dash Cooldown",
                _ => englishName
            };
        }
    }
}