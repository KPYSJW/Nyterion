using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.Data.ScriptableObjects.Synergy;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Enemy;
using System;
using UnityEngine;

namespace Nytherion.Core.Managers
{
    public class EventManager : MonoBehaviour
    {
        public event Action<EnemyBase> OnEnemyDied;
        public event Action<StageData> OnBossClearedEvent;
        public event Action<WeaponData, EngravingData, WeaponEngravingSynergyData> OnSynergyEvaluated;

        public event Action<InteractableType> OnInteraction;

        public event Action OnOpenInventoryForShop;
        public event Action OnCloseInventoryForShop;
        public void TriggerInteractionEvent(InteractableType type)
        {
            OnInteraction?.Invoke(type);
        }
        public void TriggerOpenInventoryForShop()
        {
            OnOpenInventoryForShop?.Invoke();
        }
         public void TriggerCloseInventoryForShop()
        {
            OnCloseInventoryForShop?.Invoke();
        }
        public void TriggerEnemyDeathEvent(EnemyBase enemy)
        {
            OnEnemyDied?.Invoke(enemy);
        }
        public void TriggerBossClearedEvent(StageData stage)
        {
            OnBossClearedEvent?.Invoke(stage);
        }
        public void RegisterEnemyDeathListener(Action<EnemyBase> listener) 
        {
            OnEnemyDied += listener;
        }

        public void UnregisterEnemyDeathListener(Action<EnemyBase> listener) 
        {
            OnEnemyDied -= listener;
        }
        public void RegisterBossClearedListener(Action<StageData> listener)
        {
            OnBossClearedEvent += listener;
        }
        public void UnregisterBossClearedListener(Action<StageData> listener)
        {
            OnBossClearedEvent -= listener;
        }
        public void TriggerSynergyEvaluated(WeaponData weapon, EngravingData engraving, WeaponEngravingSynergyData synergy)
        {
            OnSynergyEvaluated?.Invoke(weapon, engraving, synergy);
        }
    }
}