using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Core;

namespace Game.Characters.Enemy
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

            if (currentStageData.useRandomSpawn)
            {
                for (int i = 0; i < currentStageData.enemyCount; i++)
                {
                    int index = Random.Range(0, currentStageData.enemyList.Count);
                    EnemyData enemyData = currentStageData.enemyList[index];

                    Vector3 spawnPosition = GetRandomSpawnPosition();
                    GameObject enemyObj = ObjectPoolManager.Instance.SpawnFromPool(enemyData.enemyName, spawnPosition, Quaternion.identity);

                    var enemy = enemyObj.GetComponent<EnemyBase>();
                    if (enemy != null) enemy.Initialize(enemyData);
                }
            }
            else
            {
                for (int i = 0; i < currentStageData.fixedSpawnPoints.Count; i++)
                {
                    EnemyData enemyData = currentStageData.enemyList[i % currentStageData.enemyList.Count];
                    Vector3 spawnPosition = currentStageData.fixedSpawnPoints[i].position;

                    GameObject enemyObj = ObjectPoolManager.Instance.SpawnFromPool(enemyData.enemyName, spawnPosition, Quaternion.identity);

                    var enemy = enemyObj.GetComponent<EnemyBase>();
                    if (enemy != null) enemy.Initialize(enemyData);
                }
            }
        }

        private Vector3 GetRandomSpawnPosition()
        {
            float x = Random.Range(randomSpawnAreaMin.position.x, randomSpawnAreaMax.position.x);
            float y = Random.Range(randomSpawnAreaMin.position.y, randomSpawnAreaMax.position.y);
            return new Vector3(x, y, 0f);
        }
    }
}
