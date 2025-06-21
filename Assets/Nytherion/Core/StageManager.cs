using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.GamePlay.Characters.Enemy;

namespace Nytherion.Core
{
    public class StageManager : MonoBehaviour
    {
        public StageData[] stages;
        private int currentStageIndex = 0;
        private int remainingEnemies = 0;

        public EnemySpawner spawner;
        void Start()
        {

            if (EventManager.Instance != null)
            {
                EventManager.Instance.RegisterEnemyDeathListener(OnEnemyDied);
            }
            
            LoadStage(currentStageIndex);
        }
        private void LoadStage(int index)
        {
            if(index < 0 || index >= stages.Length) return;
            
            StageData stage = stages[index];
            remainingEnemies = stage.useRandomSpawn ? stage.enemyCount : stage.fixedSpawnPoints.Count;
            spawner.currentStageData = stage;
            spawner.SpawnEnemies();
        }
        public void OnEnemyDied()
        {
            remainingEnemies--;
            if (remainingEnemies <= 0)
            {
                StageData stage = stages[currentStageIndex];
                
                if(stage.isBossStage)
                {
                    Debug.Log($"Boss Stage 클리어!");
                }
                else
                {
                    currentStageIndex++;
                    LoadStage(currentStageIndex);
                }
            }
        }
        
        private void OnDisable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.UnregisterEnemyDeathListener(OnEnemyDied);
            }
        }
        
        private void OnDestroy()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.UnregisterEnemyDeathListener(OnEnemyDied);
            }
        }
    }

}