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
        // 테스트 도탄은 장착 유물과 별도로 관리하며 저장하지 않는다.
        private bool debugProjectileBounce;

        public event Action OnRelicsChanged;
        // 목록과 전투 스냅샷, 능력치 동기화가 끝난 뒤 효과 조건을 한 번 평가한다.
        public event Action OnRelicEffectsChanged;
        public bool IsSynchronizingRelics { get; private set; }
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

            relicManager.OnRelicStateChanged -= HandleRelicStateChanged;
            relicManager.OnRelicStateChanged += HandleRelicStateChanged;

            PublishChanges(SyncWithGrid(out _));
        }

        private void UnsubscribeRelicEvents(RelicManager targetRelicManager)
        {
            targetRelicManager.OnRelicStateChanged -= HandleRelicStateChanged;
        }

        /// <summary>
        /// 게임 시작 및 로드 시, 그리드에 장착된 각인과 플레이어의 장착 상태를 일치시킨다
        /// </summary>
        private bool SyncWithGrid(out bool hasAdded)
        {
            hasAdded = false;
            if (relicManager == null) return false;

            var placedBlocks = relicManager.GetPlacedBlocks();
            List<RelicData> placedRelics = new List<RelicData>();
            foreach (var pair in placedBlocks)
            {
                var block = relicManager.GetBlockByID(pair.Key);
                if (block != null && block.SourceData != null && !placedRelics.Contains(block.SourceData))
                {
                    placedRelics.Add(block.SourceData);
                    hasAdded |= !equippedRelics.Contains(block.SourceData);
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

        private void HandleRelicStateChanged()
        {
            bool relicListChanged = SyncWithGrid(out bool hasAdded);
            PublishChanges(relicListChanged, hasAdded);
        }

        public void AddRelic(RelicData relic)
        {
            if (equippedRelics.Count >= RelicManager.MaxEquippedRelics)
            {
                Debug.LogWarning("각인 가득참");
                return;
            }

            equippedRelics.Add(relic);
            PublishChanges(true, true);
        }

        public void RemoveRelic(int index)
        {
            if (index >= 0 && index < equippedRelics.Count)
            {
                equippedRelics.RemoveAt(index);
                PublishChanges(true);
            }
        }

        public List<RelicData> GetCurrentRelics() => equippedRelics;

        public bool IsRelicActive(string relicId)
        {
            return CombatModifiers.IsActive(relicId);
        }

        public void SetDebugProjectileBounce(bool active)
        {
            if (debugProjectileBounce == active) return;
            debugProjectileBounce = active;
            RebuildCombatModifiers();
        }

        private void RebuildCombatModifiers()
        {
            CombatModifiers = CombatModifierSnapshot.Create(equippedRelics, debugProjectileBounce);
        }

        private void PublishChanges(bool listChanged, bool hasAdded = false)
        {
            IsSynchronizingRelics = true;
            try
            {
                RebuildCombatModifiers();
                if (hasAdded)
                {
                    var weapon = playerManager?.PlayerCombat?.currentWeapon?.weaponData;
                    if (weapon != null && synergyEvaluator != null)
                        synergyEvaluator.EvaluateSynergy(weapon, equippedRelics);
                }
                if (listChanged) OnRelicsChanged?.Invoke();
            }
            finally { IsSynchronizingRelics = false; }
            OnRelicEffectsChanged?.Invoke();
        }
    }
}
