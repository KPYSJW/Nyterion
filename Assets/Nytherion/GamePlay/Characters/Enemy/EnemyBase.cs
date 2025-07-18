using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Dungeon;
using System;
using UnityEngine;
namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        public EnemyData enemyData;
        private float currentHealth;
        public bool isDead { get; private set; } = false;

        public RoomFirstDungeonGenerator.Room homeRoom { get; set; }

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
            if (isDead) return;

            isDead = true;
            DropItems();
            if (EventManager.Instance != null)
            {
                EventManager.Instance.TriggerEnemyDeathEvent(this);
            }
            else
            {
                Debug.LogWarning("EventManager.Instance is not found! The enemy death event cannot be triggered.");
            }

            gameObject.SetActive(false);
        }

        private void DropItems()
        {
            if (UnityEngine.Random.value < enemyData.dropChance)
            {
                Debug.Log($"골드 드랍: {enemyData.goldDropAmount}G ");
            }
            
        }
        private void OnCollisionEnter2D(Collision2D collision)
        {
            // 충돌한 오브젝트의 태그가 'Player'인지 확인
            if (collision.gameObject.CompareTag(Tags.Player))
            {
                Debug.Log($"{enemyData.enemyName}이(가) 플레이어와 충돌하여 즉시 사망합니다.");
                Die(); // 스스로 죽는 메서드 호출
            }
        }
    }
}
