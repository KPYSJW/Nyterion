using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Player
{
    [CreateAssetMenu(fileName = "NewPlayerData", menuName = "Data/Player")]
    public class PlayerData : ScriptableObject
    {
        public float maxHealth;
        public float moveSpeed;
        public float meleeDamage;
        public float rangedDamage;
        public float meleeSpeed;
        public float rangedSpeed;
        public float dashSpeed;
        public float dashDuration;
        public float dashDistance;
        public float dashCooldown;
        public float defense;
        public float extraProjectiles;
        public float lifesteal;
        public float chargeTimeReduction;
        public float critChance = 0.1f;
        public float critDamageMultiplier = 1.5f;
    }
}