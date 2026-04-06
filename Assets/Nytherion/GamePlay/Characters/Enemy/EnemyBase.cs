using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Dungeon;
using Nytherion.GamePlay.Items;


using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        
        public EnemyData enemyData;
        private float currentHealth;
        public bool isDead { get; private set; } = false;

        public RoomFirstDungeonGenerator.Room homeRoom { get; set; }
        private CurrencyDataManager currencyDataManager;

        private EventManager eventManager;
        [Inject]
        public void Construct(EventManager eventManager,CurrencyDataManager currencyDataManager)
        {
            this.eventManager = eventManager;
            this.currencyDataManager=currencyDataManager;
        }
        public void Initialize(EnemyData data)
        {
            enemyData = data;
            currentHealth = data.maxHealth;
            isDead = false;
            gameObject.SetActive(true);

             EnemyAIController aiController = GetComponent<EnemyAIController>();
            if (aiController != null)
            {
                aiController.ApplyEnemyData(data);
            }
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
            eventManager.TriggerEnemyDeathEvent(this);
            gameObject.SetActive(false);
        }

        private void DropItems()
        {
            
            if (Random.value <= enemyData.dropChance)
            {
                Debug.Log($"골드 드랍: {enemyData.goldDropAmount}G ");
                currencyDataManager.AddCurrency(Core.Enums.CurrencyType.Gold,10);//드랍아이템 스크립트로 옮겨야함.
                
            }

        }
       /* private void OnCollisionEnter2D(Collision2D collision)
        {
            Debug.Log(collision.gameObject.tag);
            if (collision.gameObject.CompareTag(Tags.Player)||collision.gameObject.CompareTag(Tags.Weapon))
            {
                Debug.Log($"{enemyData.enemyName}이(가) 플레이어와 충돌하여 즉시 사망합니다.");
                Die();
            }
        }*/

        private void OnTriggerEnter2D(Collider2D other) {
            Debug.Log(other.gameObject.tag);
            if (other.gameObject.CompareTag(Tags.Player)||other.gameObject.CompareTag(Tags.Weapon))
            {
                Debug.Log($"{enemyData.enemyName}이(가) 플레이어와 충돌하여 즉시 사망합니다.");
                Die();
            }
        }
    }
}
