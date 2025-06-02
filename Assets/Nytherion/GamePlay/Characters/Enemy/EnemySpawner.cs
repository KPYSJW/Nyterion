using UnityEngine;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.Core;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        public StageData currentStageData;
        
        [Header("Player Reference")]
        [SerializeField] private Transform player;
        
        [Header("Spawn Radius")]
        [Tooltip("적이 스폰될 최소 반지름")]
        [SerializeField] private float minSpawnRadius = 5f;
        [Tooltip("적이 스폰될 최대 반지름")]
        [SerializeField] private float maxSpawnRadius = 15f;

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
                Debug.LogWarning("StageData is not assigned.");
                return false;
            }

            if (currentStageData.enemyList == null || currentStageData.enemyList.Count == 0)
            {
                Debug.LogWarning("No enemies in StageData's enemy list.");
                return false;
            }

            if (currentStageData.useRandomSpawn && player == null)
            {
                Debug.LogError("Random spawn is enabled but player reference is not set.");
                return false;
            }

            if (!currentStageData.useRandomSpawn &&
                (currentStageData.fixedSpawnPoints == null || currentStageData.fixedSpawnPoints.Count == 0))
            {
                Debug.LogWarning("Fixed spawn is enabled but no spawn points are set.");
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
                    Debug.LogWarning($"Skipping null spawn point at index {i}");
                    continue;
                }

                EnemyData enemyData = currentStageData.enemyList[i % currentStageData.enemyList.Count];
                SpawnSingleEnemy(enemyData, currentStageData.fixedSpawnPoints[i].position);
            }
        }

        private void SpawnSingleEnemy(EnemyData enemyData, Vector3 spawnPosition)
        {
            if (enemyData == null) return;
            if (ObjectPoolManager.Instance == null) return;

            GameObject enemyObj = ObjectPoolManager.Instance.SpawnFromPool(
                enemyData.enemyName,
                spawnPosition,
                Quaternion.identity);

            if (enemyObj == null)
            {
                Debug.LogWarning($"Failed to spawn enemy: {enemyData.enemyName}");
                return;
            }

            EnemyBase enemy;
            if (enemyObj.TryGetComponent<EnemyBase>(out enemy))
            {
                enemy.Initialize(enemyData);
            }
            else
            {
                Debug.LogWarning($"Spawned object is not an enemy: {enemyData.enemyName}");
                ObjectPoolManager.Instance.ReturnToPool(enemyData.enemyName, enemyObj);
            }
        }

        private Vector3 GetRandomSpawnPosition()
        {
            if (player == null)
            {
                Debug.LogError("Player reference is not set in EnemySpawner!");
                return Vector3.zero;
            }

            // 랜덤한 각도와 반지름으로 위치 계산
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomRadius = Random.Range(minSpawnRadius, maxSpawnRadius);
            
            // 원형 범위 내 랜덤 위치 계산
            Vector2 offset = new Vector2(
                Mathf.Cos(randomAngle) * randomRadius,
                Mathf.Sin(randomAngle) * randomRadius
            );

            Vector3 spawnPos = (Vector3)offset + player.position;
            spawnPos.z = 0f; // 2D 게임이므로 z값은 0으로 고정
            
            return spawnPos;
        }
    }
}
