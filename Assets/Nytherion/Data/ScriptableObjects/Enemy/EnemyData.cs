using UnityEngine;
using Nytherion.Core.Utils;

namespace Nytherion.Data.ScriptableObjects.Enemy
{
    public enum EnemyCombatType
    {
        Melee,
        Ranged,
        Hybrid
    }

    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "Data/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string enemyName;
        public string DisplayName => LocalizationText.Get(
            LocalizationTables.World,
            LocalizationKeys.EnemyName(name),
            enemyName,
            enemyName);
        public GameObject enemyPrefab;

        [Header("Base Stats")]
        public float maxHealth;
        public float moveSpeed;
        public int damageAmount;
        public float detectRange = 8f;

        [Header("Combat Type")]
        public EnemyCombatType combatType = EnemyCombatType.Melee;
        public float hybridSwitchDistance = 2.5f;

        [Header("Drop")]
        [Range(0f, 1f)] public float dropChance;
        public int goldDropAmount;
    }
}
