using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Data;
using VContainer;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public class ItemUsageManager : BaseManager
    {

        private InventoryManager inventoryManager;
        private PlayerHealth playerHealth;

        [Inject]
        public void Construct(
            InventoryManager inventoryManager,
            PlayerHealth playerHealth)
        {
            this.inventoryManager = inventoryManager;
            this.playerHealth = playerHealth;
        }

        protected override void OnInitializeInternal()
        {

        }
        public void UseConsumableItem(ConsumableData consumable)
        {
            if (consumable == null)
            {
                return;
            }

            if (inventoryManager.RemoveItem(consumable, 1))
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
        
        public override void PopulateSaveData(SaveData saveData)
        {

        }
        
        public override void LoadFromSaveData(SaveData saveData)
        {

        }
    }
}