using UnityEngine;
using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.GamePlay.Systems;
using Zenject;
using Nytherion.GamePlay.Characters.Enemy;

namespace Nytherion.Core.Managers
{
    public class StageManager : MonoBehaviour
    {
        public StageData[] stages;
        private int currentStageIndex = 0;
        private int remainingEnemies = 0;

        private EnemySpawner spawner;
        private EventManager eventManager;

        [Inject]
        public void Construct(EnemySpawner enemySpawner, EventManager eventManager)
        {
            spawner = enemySpawner;
            this.eventManager = eventManager;
        }
        void Start()
        {
            if (eventManager != null)
            {

                eventManager.RegisterEnemyDeathListener(OnEnemyDied);
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
        public void OnEnemyDied(EnemyBase enemy)
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
            if (eventManager != null)
            {

                eventManager.UnregisterEnemyDeathListener(OnEnemyDied);
            }
        }
        
        private void OnDestroy()
        {
            // Unregister is already handled in OnDisable
        }
    }

}