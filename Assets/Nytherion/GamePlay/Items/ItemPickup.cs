using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using VContainer;
using Nytherion.Core.Enums;

public enum PickupType
{
    Gold,
    Item
}

namespace Nytherion.GamePlay.Items
{
    public class ItemPickup : MonoBehaviour
    {
        public ItemData itemData;
        [SerializeField] private int goldAmount;
        [SerializeField] private PickupType pickupType;
        private InventoryDataManager inventoryDataManager;
        private CurrencyDataManager currencyDataManager;

        [Inject]
        public void Construct(InventoryDataManager inventoryDataManager, CurrencyDataManager currencyDataManager)
        {
            this.inventoryDataManager = inventoryDataManager;
            this.currencyDataManager = currencyDataManager;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
           if (!other.CompareTag(Tags.Player))
             return;
            switch (pickupType)
            {
                case PickupType.Item:
                    if (itemData != null && inventoryDataManager.AddItem(itemData, 1))
                    {
                        Destroy(gameObject);
                    }
                    break;

                case PickupType.Gold:
                    if (goldAmount > 0 && currencyDataManager.AddCurrency(CurrencyType.Gold, goldAmount))
                    {
                        Destroy(gameObject);
                    }
                    break;
            }
        }
    }
}
