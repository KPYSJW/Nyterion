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
        private bool hasStarted;

        public event Action OnRelicsChanged;
        public CombatModifierSnapshot CombatModifiers { get; private set; } = CombatModifierSnapshot.Empty;

        [Inject]
        public void Construct(EventManager eventManager, RelicManager relicManager)
        {
            if (this.relicManager != null && this.relicManager != relicManager)
            {
                UnsubscribeRelicEvents(this.relicManager);
            }

            this.eventManager = eventManager;
            this.relicManager = relicManager;

            if (hasStarted)
            {
                InitializeRuntimeDependencies();
            }
        }

        private void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
        }

        private void Start()
        {
            hasStarted = true;
            if (playerManager == null) playerManager = GetComponent<PlayerManager>();
            InitializeRuntimeDependencies();
        }

        private void OnDestroy()
        {
            if (relicManager != null)
            {
                UnsubscribeRelicEvents(relicManager);
            }
        }

        private void InitializeRuntimeDependencies()
        {
            if (eventManager != null)
            {
                synergyEvaluator = new SynergyEvaluator(synergyTable, eventManager);
            }

            if (relicManager == null) return;

            relicManager.OnRelicEquippedStateChanged -= HandleRelicStateFromGrid;
            relicManager.OnRelicEquippedStateChanged += HandleRelicStateFromGrid;
            relicManager.OnRelicStateChanged -= HandleRelicStateChanged;
            relicManager.OnRelicStateChanged += HandleRelicStateChanged;

            bool relicListChanged = SyncWithGrid();
            RebuildCombatModifiers();
            if (relicListChanged)
            {
                OnRelicsChanged?.Invoke();
            }
        }

        private void UnsubscribeRelicEvents(RelicManager targetRelicManager)
        {
            targetRelicManager.OnRelicEquippedStateChanged -= HandleRelicStateFromGrid;
            targetRelicManager.OnRelicStateChanged -= HandleRelicStateChanged;
        }

        /// <summary>
        /// 게임 시작 및 로드 시, 그리드에 장착된 각인과 플레이어의 장착 상태를 일치시킨다
        /// </summary>
        private bool SyncWithGrid()
        {
            if (relicManager == null) return false;

            var placedBlocks = relicManager.GetPlacedBlocks();
            List<RelicData> placedRelics = new List<RelicData>();
            foreach (var pair in placedBlocks)
            {
                var block = relicManager.GetBlockByID(pair.Key);
                if (block != null && block.SourceData != null && !placedRelics.Contains(block.SourceData))
                {
                    placedRelics.Add(block.SourceData);
                }
            }

            bool hasChanged = equippedRelics.Count != placedRelics.Count;
            if (!hasChanged)
            {
                for (int i = 0; i < placedRelics.Count; i++)
                {
                    if (!equippedRelics.Contains(placedRelics[i]))
                    {
                        hasChanged = true;
                        break;
                    }
                }
            }

            if (!hasChanged) return false;

            // 저장 데이터 로드 시 RelicManager가 기존 그리드를 한 번에 비우므로 개별 해제 이벤트가
            // 오지 않을 수 있습니다. 현재 그리드를 기준으로 목록을 재구성해 해제된 유물이 남지 않게 합니다.
            equippedRelics.Clear();
            equippedRelics.AddRange(placedRelics);
            return true;
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

        private void HandleRelicStateChanged()
        {
            bool relicListChanged = SyncWithGrid();
            RebuildCombatModifiers();
            if (relicListChanged)
            {
                OnRelicsChanged?.Invoke();
            }
        }

        public void AddRelic(RelicData relic)
        {
            if (equippedRelics.Count >= RelicManager.MaxEquippedRelics)
            {
                Debug.LogWarning("각인 가득참");
                return;
            }

            equippedRelics.Add(relic);
            RebuildCombatModifiers();

            var currentWeaponData = playerManager?.PlayerCombat?.currentWeapon?.weaponData;
            if (currentWeaponData != null && synergyEvaluator != null)
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
                RebuildCombatModifiers();

                OnRelicsChanged?.Invoke();
            }
        }

        public List<RelicData> GetCurrentRelics() => equippedRelics;

        public bool IsRelicActive(string relicId)
        {
            return CombatModifiers.IsActive(relicId);
        }

        private void RebuildCombatModifiers()
        {
            CombatModifiers = CombatModifierSnapshot.Create(equippedRelics);
        }
    }
}
