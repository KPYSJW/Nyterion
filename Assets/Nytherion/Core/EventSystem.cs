using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Nytherion.Data;

namespace Nytherion.Core
{
    public class EventSystem : MonoBehaviour
    {
       public static EventSystem Instance { get; private set; }
       private void Awake()
       {
           if (Instance == null) Instance = this;
           else Destroy(gameObject);
       }

       public event Action OnEnemyDeathEvent;
       public event Action<StageData> OnBossClearedEvent;
       public void TriggerEnemyDeathEvent(EnemyData data)
       {
           OnEnemyDeathEvent?.Invoke();
       }
       public void TriggerBossClearedEvent(StageData stage)
       {
           OnBossClearedEvent?.Invoke(stage);
       }
       public void RegisterEnemyDeathListener(Action listener)
       {
           OnEnemyDeathEvent += listener;
       }
       public void UnregisterEnemyDeathListener(Action listener)
       {
           OnEnemyDeathEvent -= listener;
       }
       public void RegisterBossClearedListener(Action<StageData> listener)
       {
           OnBossClearedEvent += listener;
       }
       public void UnregisterBossClearedListener(Action<StageData> listener)
       {
           OnBossClearedEvent -= listener;
       }
    }
}