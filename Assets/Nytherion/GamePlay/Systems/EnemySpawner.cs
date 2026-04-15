using UnityEngine;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using VContainer;

namespace Nytherion.GamePlay.Systems
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        public StageData currentStageData;
        
        private Transform player;
        private ObjectPoolManager poolManager;
        
        [Header("Spawn Radius")]
        [Tooltip("적이 스폰될 최소 반지름")]
        [SerializeField] private float minSpawnRadius = 5f;
        [Tooltip("적이 스폰될 최대 반지름")]
        [SerializeField] private float maxSpawnRadius = 15f;

        [Inject]
        public void Construct(PlayerController playerController, ObjectPoolManager objectPoolManager)
        {
            player = playerController.transform;
            poolManager = objectPoolManager;
        }

        public void SpawnEnemies()
        {
            if (!ValidateSpawnConditions()) return;

            if (currentStageData.useRandomSpawn)
            {
                SpawnRandomEnemies();
            }
            else
            {
                SpawnAtFixedPoints();
            }
        }

        private bool ValidateSpawnConditions()
        {
            if (currentStageData == null)
            {
                return false;
            }

            if (currentStageData.enemyList == null || currentStageData.enemyList.Count == 0)
            {
                return false;
            }

            if (currentStageData.useRandomSpawn && player == null)
            {
                return false;
            }

            if (!currentStageData.useRandomSpawn &&
                (currentStageData.fixedSpawnPoints == null || currentStageData.fixedSpawnPoints.Count == 0))
            {
                return false;
            }

            return true;
        }

        private void SpawnRandomEnemies()
        {
            for (int i = 0; i < currentStageData.enemyCount; i++)
            {
                EnemyData enemyData = currentStageData.enemyList[Random.Range(0, currentStageData.enemyList.Count)];
                SpawnSingleEnemy(enemyData, GetRandomSpawnPosition());
            }
        }

        private void SpawnAtFixedPoints()
        {
            for (int i = 0; i < currentStageData.fixedSpawnPoints.Count; i++)
            {
                if (currentStageData.fixedSpawnPoints[i] == null)
                {
                    continue;
                }

                EnemyData enemyData = currentStageData.enemyList[i % currentStageData.enemyList.Count];
                SpawnSingleEnemy(enemyData, currentStageData.fixedSpawnPoints[i].position);
            }
        }

        private void SpawnSingleEnemy(EnemyData enemyData, Vector3 spawnPosition)
        {
            if (enemyData == null) return;
            if (poolManager == null) return;

            GameObject enemyObj = poolManager.SpawnFromPool(
                enemyData.enemyName,
                spawnPosition,
                Quaternion.identity);

            if (enemyObj == null)
            {
                return;
            }

            EnemyBase enemy;
            if (enemyObj.TryGetComponent<EnemyBase>(out enemy))
            {
                enemy.Initialize(enemyData);
            }
            else
            {
                poolManager.ReturnToPool(enemyData.enemyName, enemyObj);
            }
        }

        private Vector3 GetRandomSpawnPosition()
        {
            if (player == null)
            {
                return Vector3.zero;
            }

            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomRadius = Random.Range(minSpawnRadius, maxSpawnRadius);
            
            Vector2 offset = new Vector2(
                Mathf.Cos(randomAngle) * randomRadius,
                Mathf.Sin(randomAngle) * randomRadius
            );

            Vector3 spawnPos = (Vector3)offset + player.position;
            spawnPos.z = 0f;
            
            return spawnPos;
        }
    }
}
