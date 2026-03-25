using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Synergy;
using Nytherion.GamePlay.Combat;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using System;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerEngravingManager : MonoBehaviour
    {
        [SerializeField] public List<EngravingData> equippedEngravings = new List<EngravingData>();
        [SerializeField] public List<WeaponEngravingSynergyData> synergyTable;
        public SynergyEvaluator synergyEvaluator;

        private PlayerManager playerManager;
        private EngravingManager engravingManager;
        private EventManager eventManager;

        public event Action OnEngravingsChanged;

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
            if (engravingManager == null) engravingManager = FindObjectOfType<EngravingManager>();

            if (engravingManager != null)
            {
                engravingManager.OnEngravingEquippedStateChanged -= HandleEngravingStateFromGrid;
                engravingManager.OnEngravingEquippedStateChanged += HandleEngravingStateFromGrid;


                SyncWithGrid();
            }
            else
            {
                Debug.LogError(" [디버그] EngravingManager를 찾을 수 없어 이벤트 구독에 실패했습니다.");
            }
        }

        private void OnDestroy()
        {
            if (engravingManager != null)
            {
                engravingManager.OnEngravingEquippedStateChanged -= HandleEngravingStateFromGrid;
            }
        }

        /// <summary>
        /// 게임 시작 및 로드 시, 그리드에 장착된 각인과 플레이어의 장착 상태를 일치시킵니다.
        /// </summary>
        private void SyncWithGrid()
        {
            if (engravingManager == null) return;

            var placedBlocks = engravingManager.GetPlacedBlocks();
            foreach (var pair in placedBlocks)
            {
                var block = engravingManager.GetBlockByID(pair.Key);
                if (block != null && block.SourceData != null)
                {
                    if (!equippedEngravings.Contains(block.SourceData))
                    {
                        AddEngraving(block.SourceData);
                    }
                }
            }
        }

        private void HandleEngravingStateFromGrid(EngravingData data, bool isEquipped)
        {
            Debug.Log($" [디버그] 각인 이벤트 수신 완료 - 대상: {data.engravingName}, 장착여부: {isEquipped}");

            if (isEquipped)
            {
                // 중복 체크 후 장착
                if (!equippedEngravings.Contains(data))
                {
                    AddEngraving(data);
                }
            }
            else
            {
                // 이름으로 찾아서 해제
                int indexToRemove = equippedEngravings.FindIndex(e => e.engravingName == data.engravingName);
                if (indexToRemove != -1)
                {
                    RemoveEngraving(indexToRemove);
                }
            }
        }

        public void AddEngraving(EngravingData engraving)
        {
            if (equippedEngravings.Count >= 3)
            {
                Debug.LogWarning("각인 가득참");
                return;
            }

            equippedEngravings.Add(engraving);
            Debug.Log($" [디버그] 각인 플레이어 스탯에 추가됨: {engraving.engravingName}");

            var currentWeaponData = playerManager?.PlayerCombat?.currentWeapon?.weaponData;
            if (currentWeaponData != null)
            {
                WeaponEngravingSynergyData SynergyData = synergyEvaluator.EvaluateSynergy(currentWeaponData, GetCurrentEngravings());
                if (SynergyData != null)
                {
                    Debug.Log($" 시너지 발동: {SynergyData.weaponName} + {SynergyData.engravingName}");
                }
            }

            OnEngravingsChanged?.Invoke();
        }

        public void RemoveEngraving(int index)
        {
            if (index >= 0 && index < equippedEngravings.Count)
            {
                Debug.Log($" [디버그] 각인 플레이어 스탯에서 제거됨: {equippedEngravings[index].engravingName}");
                equippedEngravings.RemoveAt(index);

                OnEngravingsChanged?.Invoke();
            }
        }

        public List<EngravingData> GetCurrentEngravings() => equippedEngravings;
    }
}