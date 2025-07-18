using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;

namespace Nytherion.GamePlay.Items
{
    public class ItemPickup : MonoBehaviour
    {
        public ItemData itemData;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(Tags.Player))
            {
                if (InventoryManager.Instance.AddItem(itemData))
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
