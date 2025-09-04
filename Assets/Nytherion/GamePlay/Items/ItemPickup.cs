using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Zenject;

namespace Nytherion.GamePlay.Items
{
    public class ItemPickup : MonoBehaviour
    {
        public ItemData itemData;
        private InventoryManager inventoryManager;

        [Inject]
        public void Construct(InventoryManager inventoryManager)
        {
            this.inventoryManager = inventoryManager;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(Tags.Player))
            {
                if (inventoryManager.AddItem(itemData))
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
