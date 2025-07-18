using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.Core.Managers
{
    public class ItemUsageManager : MonoBehaviour
    {
        public static ItemUsageManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void UseConsumableItem(ConsumableData consumable)
        {
            if (consumable == null)
            {
                return;
            }

            if (InventoryManager.Instance.RemoveItem(consumable, 1))
            {
                ApplyItemEffect(consumable);
            }
        }

        private void ApplyItemEffect(ConsumableData consumable)
        {
            if (consumable.useSound != null)
            {
                // 사운드 로직
            }
            if (consumable.useEffectPrefab != null)
            {
                // 이펙트 로직
            }

            switch (consumable.consumableType)
            {
                case ConsumableType.HealthPotion:
                    UseHealthPotion(consumable);
                    break;
                case ConsumableType.Buff:
                    ApplyBuff(consumable);
                    break;
                case ConsumableType.Throwable:
                    ThrowItem(consumable);
                    break;
            }
        }

        private void UseHealthPotion(ConsumableData potion)
        {
            PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Heal(potion.healAmount);
            }
        }

        private void ApplyBuff(ConsumableData buffItem)
        {
            // 버프 로직
        }

        private void ThrowItem(ConsumableData throwableItem)
        {
            if (throwableItem.projectilePrefab != null)
            {
                // 투척 로직
            }
        }
    }
}