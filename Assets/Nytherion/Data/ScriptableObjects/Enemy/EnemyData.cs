using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Enemy
{
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "Data/Enemy")]
    public class EnemyData : ScriptableObject
    {
        public string enemyName;
        public float maxHealth;
        public float moveSpeed;
        public int damageAmount;
        public float dropChance;
        public int goldDropAmount;
        public GameObject enemyPrefab;
    }
}
