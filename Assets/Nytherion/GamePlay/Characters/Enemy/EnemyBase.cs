using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Core;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        public EnemyData enemyData;
        private float currentHealth;
        private bool isDead = false;

        public void Initialize(EnemyData data)
        {
            enemyData = data;
            currentHealth = data.maxHealth;
            isDead = false;
            gameObject.SetActive(true);
        }

        public void TakeDamage(float damageAmount)
        {
            if (isDead) return;

            currentHealth -= damageAmount;
            if (currentHealth <= 0) Die();
        }

        private void Die()
        {
            isDead = true;
            DropItems();
            EventSystem.Instance.TriggerEnemyDeathEvent(enemyData);
            gameObject.SetActive(false);
        }

        private void DropItems()
        {
            if (Random.value < enemyData.dropChance)
            {
                Debug.Log($"골드 드랍: {enemyData.goldDropAmount}G (CurrencyManager 미구현)");
            }
            
        }
    }
}
