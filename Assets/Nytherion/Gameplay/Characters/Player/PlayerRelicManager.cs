using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Synergy;
using Nytherion.GamePlay.Combat;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using System;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerRelicManager : MonoBehaviour
    {
        [SerializeField] public List<RelicData> equippedRelics = new List<RelicData>();
        [SerializeField] public List<WeaponRelicSynergyData> synergyTable;
        public SynergyEvaluator synergyEvaluator;

        private PlayerManager playerManager;
        private RelicManager relicManager;
        private EventManager eventManager;

        public event Action OnRelicsChanged;

        [Inject]
        public void Construct(EventManager eventManager)
        {
            this.eventManager = eventManager;
        }

        private void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
        }

        private void Start()
        {
            synergyEvaluator = new SynergyEvaluator(synergyTable, eventManager);

            if (playerManager == null) playerManager = GetComponent<PlayerManager>();
            if (relicManager == null) relicManager = FindObjectOfType<RelicManager>();

            if (relicManager != null)
            {
                relicManager.OnRelicEquippedStateChanged -= HandleRelicStateFromGrid;
                relicManager.OnRelicEquippedStateChanged += HandleRelicStateFromGrid;


                SyncWithGrid();
            }
            else
            {
                Debug.LogError(" [디버그] RelicManager를 찾을 수 없어 이벤트 구독에 실패했습니다.");
            }
        }

        private void OnDestroy()
        {
            if (relicManager != null)
            {
                relicManager.OnRelicEquippedStateChanged -= HandleRelicStateFromGrid;
            }
        }

        /// <summary>
        /// 게임 시작 및 로드 시, 그리드에 장착된 각인과 플레이어의 장착 상태를 일치시킨다
        /// </summary>
        private void SyncWithGrid()
        {
            if (relicManager == null) return;

            var placedBlocks = relicManager.GetPlacedBlocks();
            foreach (var pair in placedBlocks)
            {
                var block = relicManager.GetBlockByID(pair.Key);
                if (block != null && block.SourceData != null)
                {
                    if (!equippedRelics.Contains(block.SourceData))
                    {
                        AddRelic(block.SourceData);
                    }
                }
            }
        }

        private void HandleRelicStateFromGrid(RelicData data, bool isEquipped)
        {

            if (isEquipped)
            {
                // 중복 체크 후 장착
                if (!equippedRelics.Contains(data))
                {
                    AddRelic(data);
                }
            }
            else
            {
                // 이름으로 찾아서 해제
                int indexToRemove = equippedRelics.FindIndex(e => e.relicName == data.relicName);
                if (indexToRemove != -1)
                {
                    RemoveRelic(indexToRemove);
                }
            }
        }

        public void AddRelic(RelicData relic)
        {
            if (equippedRelics.Count >= 25)
            {
                Debug.LogWarning("각인 가득참");
                return;
            }

            equippedRelics.Add(relic);

            var currentWeaponData = playerManager?.PlayerCombat?.currentWeapon?.weaponData;
            if (currentWeaponData != null)
            {
                WeaponRelicSynergyData SynergyData = synergyEvaluator.EvaluateSynergy(currentWeaponData, GetCurrentRelics());
                if (SynergyData != null)
                {
                    Debug.Log($" 시너지 발동: {SynergyData.weaponName} + {SynergyData.relicName}");
                }
            }

            OnRelicsChanged?.Invoke();
        }

        public void RemoveRelic(int index)
        {
            if (index >= 0 && index < equippedRelics.Count)
            {
                equippedRelics.RemoveAt(index);

                OnRelicsChanged?.Invoke();
            }
        }

        public List<RelicData> GetCurrentRelics() => equippedRelics;

        public bool IsRelicActive(string relicId)
        {
            if (string.IsNullOrEmpty(relicId)) return false;

            if (relicManager == null)
            {
                relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
            }

            if (relicManager != null && relicManager.IsRelicActive(relicId))
            {
                return true;
            }

            string targetId = relicId.Trim();
            foreach (RelicData data in equippedRelics)
            {
                if (data != null && !data.isDisabled)
                {
                    string dataName = data.name != null ? data.name.Trim() : "";
                    string relicName = data.relicName != null ? data.relicName.Trim() : "";

                    if (string.Equals(dataName, targetId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(relicName, targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}