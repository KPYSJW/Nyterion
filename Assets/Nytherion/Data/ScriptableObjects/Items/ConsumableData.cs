using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Items
{
    public enum ConsumableType
    {
        HealthPotion,
        Buff,
        Throwable
    }

    [CreateAssetMenu(fileName = "NewConsumableData", menuName = "Data/Item/Consumable")]
    public class ConsumableData : ItemData
    {
        [Header("Usage Settings")]
        public bool isUsable = true;
        public GameObject useEffectPrefab;
        public AudioClip useSound;

        [Header("Consumable Specifics")]
        public ConsumableType consumableType;

        public float healAmount;
        public float buffDuration;

        public GameObject projectilePrefab;
    }
}