using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.Data.ScriptableObjects.Synergy;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.Core.Data;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public class EventManager : BaseManager
    {
        public event Action<EnemyBase> OnEnemyDied;
        public event Action<float> OnEnemyDamagedByPlayer;
        public event Action<StageData> OnBossClearedEvent;
        public event Action<WeaponData, EngravingData, WeaponEngravingSynergyData> OnSynergyEvaluated;

        public event Action<Vector2, int, float, Transform, string> OnPlayerRangedAttack;

        public event Action<InteractableType> OnInteraction;

        public event Action OnOpenInventoryForShop;
        public event Action OnCloseInventoryForShop;

        public void TriggerPlayerRangedAttack(Vector2 direction, int projectileCount, float baseDamage, Transform firePoint, string poolTag)
        {
            OnPlayerRangedAttack?.Invoke(direction, projectileCount, baseDamage, firePoint, poolTag);
        }

        public void TriggerInteractionEvent(InteractableType type)
        {
            OnInteraction?.Invoke(type);
        }
        public void TriggerEnemyDamagedByPlayer(float damageAmount)
        {
            OnEnemyDamagedByPlayer?.Invoke(damageAmount);
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

        public void TriggerEvent(string eventName, object eventData = null)
        {
            // TODO: Implement generic event system if needed
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            // EventManager는 저장할 데이터가 없음
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            // EventManager는 로드할 데이터가 없음
        }
    }
}