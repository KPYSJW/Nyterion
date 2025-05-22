using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.Core;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemySpawner : MonoBehaviour
    {
        public StageData currentStageData;
        public Transform randomSpawnAreaMin;
        public Transform randomSpawnAreaMax;

        public void SpawnEnemies()
        {
            if (currentStageData == null)
            {
                Debug.LogWarning("StageData is null.");
                return;
            }

            if (currentStageData.enemyList == null || currentStageData.enemyList.Count == 0)
            {
                Debug.LogWarning("EnemyList in StageData is null or empty. Cannot spawn enemies.");
                return;
            }
            
            if (randomSpawnAreaMin == null || randomSpawnAreaMax == null)
            {
                Debug.LogWarning("Spawn area bounds (randomSpawnAreaMin or randomSpawnAreaMax) are not set.");
                // Optionally, you could prevent random spawning if this is the case or use a default area.
                // For now, we'll let it potentially fail in GetRandomSpawnPosition if useRandomSpawn is true and these are needed.
            }


            if (currentStageData.useRandomSpawn)
            {
                if (randomSpawnAreaMin == null || randomSpawnAreaMax == null)
                {
                    Debug.LogError("Cannot use random spawn because spawn area bounds (randomSpawnAreaMin or randomSpawnAreaMax) are not set.");
                    return;
                }
                for (int i = 0; i < currentStageData.enemyCount; i++)
                {
                    int index = Random.Range(0, currentStageData.enemyList.Count);
                    EnemyData enemyData = currentStageData.enemyList[index];
                    Vector3 spawnPosition = GetRandomSpawnPosition();
                    SpawnSingleEnemy(enemyData, spawnPosition);
                }
            }
            else
            {
                if (currentStageData.fixedSpawnPoints == null || currentStageData.fixedSpawnPoints.Count == 0)
                {
                     Debug.LogWarning("Fixed spawn points list in StageData is null or empty, but useRandomSpawn is false. Cannot spawn enemies.");
                     return;
                }
                for (int i = 0; i < currentStageData.fixedSpawnPoints.Count; i++)
                {
                    if(currentStageData.fixedSpawnPoints[i] == null)
                    {
                        Debug.LogWarning($"Fixed spawn point at index {i} is null. Skipping this spawn point.");
                        continue;
                    }
                    // Ensure enemyList has elements before trying to access it with modulo.
                    // This is already checked by the general enemyList check at the beginning of the method.
                    EnemyData enemyData = currentStageData.enemyList[i % currentStageData.enemyList.Count];
                    Vector3 spawnPosition = currentStageData.fixedSpawnPoints[i].position;
                    SpawnSingleEnemy(enemyData, spawnPosition);
                }
            }
        }

        private void SpawnSingleEnemy(EnemyData enemyData, Vector3 spawnPosition)
        {
            if (enemyData == null)
            {
                Debug.LogWarning("EnemyData is null. Cannot spawn enemy.");
                return;
            }

            if (ObjectPoolManager.Instance == null)
            {
                Debug.LogError("ObjectPoolManager.Instance is null. Cannot spawn enemy.");
                return;
            }

            GameObject enemyObj = ObjectPoolManager.Instance.SpawnFromPool(enemyData.enemyName, spawnPosition, Quaternion.identity);

            if (enemyObj == null)
            {
                Debug.LogWarning($"Failed to spawn enemy {enemyData.enemyName} from pool. It might be empty, not exist, or prefab is missing in pool.");
                return;
            }

            var enemy = enemyObj.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.Initialize(enemyData);
            }
            else
            {
                Debug.LogWarning($"Spawned object {enemyData.enemyName} (Instance ID: {enemyObj.GetInstanceID()}) does not have an EnemyBase component. Returning to pool.");
                ObjectPoolManager.Instance.ReturnToPool(enemyData.enemyName, enemyObj); // Return to pool if not a valid enemy
            }
        }

        private Vector3 GetRandomSpawnPosition()
        {
            if (randomSpawnAreaMin == null || randomSpawnAreaMax == null)
            {
                Debug.LogError("Spawn area bounds (randomSpawnAreaMin or randomSpawnAreaMax) are not set. Returning origin.");
                return Vector3.zero; // Fallback position
            }
            float x = Random.Range(randomSpawnAreaMin.position.x, randomSpawnAreaMax.position.x);
            float y = Random.Range(randomSpawnAreaMin.position.y, randomSpawnAreaMax.position.y);
            return new Vector3(x, y, 0f);
        }
    }
}
