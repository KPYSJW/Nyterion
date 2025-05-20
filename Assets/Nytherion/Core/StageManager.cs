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
            else
            {
                Debug.LogError("EventManager.Instance is not available!");
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
            Debug.Log($"Stage {stage.stageName} 시작, 적 수: {remainingEnemies}");
        }
        public void OnEnemyDied()
        {
            remainingEnemies--;
            if (remainingEnemies <= 0)
            {
                Debug.Log($"Stage {stages[currentStageIndex].stageName} 클리어!");
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
            // 오브젝트가 파괴될 때도 이벤트 리스너 해제
            if (EventManager.Instance != null)
            {
                EventManager.Instance.UnregisterEnemyDeathListener(OnEnemyDied);
            }
        }
    }

}