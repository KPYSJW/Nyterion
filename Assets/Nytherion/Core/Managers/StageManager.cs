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
        private EventManager _eventManager;

        [Inject]
        public void Construct(EnemySpawner enemySpawner, EventManager eventManager)
        {
            spawner = enemySpawner;
            _eventManager = eventManager;
        }
        void Start()
        {
            if (_eventManager != null)
            {

                _eventManager.RegisterEnemyDeathListener(OnEnemyDied);
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
            if (_eventManager != null)
            {

                _eventManager.UnregisterEnemyDeathListener(OnEnemyDied);
            }
        }
        
        private void OnDestroy()
        {
            // Unregister is already handled in OnDisable
        }
    }

}