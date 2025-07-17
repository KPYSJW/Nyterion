using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Synergy;
using Nytherion.GamePlay.Combat;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerEngravingManager : MonoBehaviour
    {
        [SerializeField] public List<EngravingData> equippedEngravings = new List<EngravingData>();
        [SerializeField] public List<WeaponEngravingSynergyData> synergyTable;
        public SynergyEvaluator synergyEvaluator;

        private PlayerManager _playerManager;

        private void Awake()
        {
            _playerManager = GetComponent<PlayerManager>();
            synergyEvaluator = new SynergyEvaluator(synergyTable);
        }

        public void AddEngraving(EngravingData engraving)
        {
            if (equippedEngravings.Count >= 3)
            {
                Debug.Log("각인 가득참");
                return;
            }
            equippedEngravings.Add(engraving);
            WeaponEngravingSynergyData SynergyData = synergyEvaluator.EvaluateSynergy(_playerManager.PlayerCombat.currentWeapon.weaponData, GetCurrentEngravings());
            if (SynergyData != null)
            {
                Debug.Log($"✅ 시너지 발동: {SynergyData.weaponName} + {SynergyData.engravingName}");
            }
            else
            {
                Debug.Log("❌ 시너지 없음.");
            }
            EngravingStat(engraving);
        }
        public void RemoveEngraving(int index)
        {
            if (index >= 0 && index <= 3)
            {
                equippedEngravings.RemoveAt(index);
            }
        }

        public List<EngravingData> GetCurrentEngravings() => equippedEngravings;

        public void EngravingStat(EngravingData engraving)
        {
            _playerManager.currentPlayerData.meleeDamage += 1;
        }
    }
}