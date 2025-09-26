using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using VContainer;

namespace Nytherion.GamePlay.Items
{
    public class ItemPickup : MonoBehaviour
    {
        public ItemData itemData;
        private InventoryDataManager inventoryDataManager;

        [Inject]
        public void Construct(InventoryDataManager inventoryDataManager)
        {
            this.inventoryDataManager = inventoryDataManager;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(Tags.Player))
            {
                if (inventoryDataManager.AddItem(itemData, 1))
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
